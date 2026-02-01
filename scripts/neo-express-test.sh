#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

NEOXP="${NEOXP:-}"
if [[ -z "${NEOXP}" ]]; then
  if command -v neoxp >/dev/null 2>&1; then
    NEOXP="neoxp"
  elif [[ -x "${HOME}/.dotnet/tools/neoxp" ]]; then
    NEOXP="${HOME}/.dotnet/tools/neoxp"
  else
    echo "neoxp not found (set NEOXP or install Neo.Express)" >&2
    exit 1
  fi
fi

NCCS="${NCCS:-}"
if [[ -z "${NCCS}" ]]; then
  if command -v nccs >/dev/null 2>&1; then
    NCCS="nccs"
  elif [[ -x "${HOME}/.dotnet/tools/nccs" ]]; then
    NCCS="${HOME}/.dotnet/tools/nccs"
  else
    echo "nccs not found (set NCCS or install neo.compiler.csharp)" >&2
    exit 1
  fi
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required for JSON parsing." >&2
  exit 1
fi

EXPRESS_DIR="${EXPRESS_DIR:-"${ROOT}/.neo-express"}"
EXPRESS_FILE="${EXPRESS_DIR}/trustanchor.neo-express"
BUILD_DIR="${EXPRESS_DIR}/build"
INVOKE_DIR="${EXPRESS_DIR}/invoke"

DEV_PRIVATE_KEY="${DEV_PRIVATE_KEY:-171447AFA470EDC03AF9330F4044C592800D9BAD0B7D8078B4A35A31A58C8F69}"
DEV2_PRIVATE_KEY="${DEV2_PRIVATE_KEY:-C5E2C9D1C0C7A3CEB1B0F9F1E6B20A7C7A4CBA7B7AE7D9E2F1E9C0D1B2A3C4D5}"
PASSWORD="${PASSWORD:-password}"

mkdir -p "${BUILD_DIR}" "${INVOKE_DIR}"

"${NEOXP}" create -f -o "${EXPRESS_FILE}" -p "${DEV_PRIVATE_KEY}" >/dev/null
"${NEOXP}" reset -i "${EXPRESS_FILE}" -a -f >/dev/null
"${NEOXP}" wallet create -i "${EXPRESS_FILE}" -p "${DEV_PRIVATE_KEY}" -f dev >/dev/null
"${NEOXP}" wallet create -i "${EXPRESS_FILE}" -p "${DEV2_PRIVATE_KEY}" -f dev2 >/dev/null

OWNER_HASH="$("${NEOXP}" wallet list -i "${EXPRESS_FILE}" -j | jq -r '.dev[0]."script-hash"')"

CANDIDATE_PUBKEYS=()
for i in $(seq 1 21); do
  key="$(printf "%064x" "${i}")"
  name="candidate${i}"
  "${NEOXP}" wallet create -i "${EXPRESS_FILE}" -p "${key}" -f "${name}" >/dev/null
  pubkey="$("${NEOXP}" wallet list -i "${EXPRESS_FILE}" -j | jq -r --arg name "${name}" '.[$name][0]."public-key"')"
  CANDIDATE_PUBKEYS+=("${pubkey}")
done

mkdir -p "${BUILD_DIR}/TrustAnchor"
shopt -s nullglob
TRUST_SOURCES=("${ROOT}/contract/TrustAnchor"*.cs)
shopt -u nullglob
if [[ ${#TRUST_SOURCES[@]} -eq 0 ]]; then
  echo "TrustAnchor sources not found in ${ROOT}/contract" >&2
  exit 1
fi
TRUST_BUILD_SOURCES=()
for source in "${TRUST_SOURCES[@]}"; do
  base="$(basename "${source}")"
  if [[ "${base}" == "TrustAnchorAgent.cs" ]]; then
    continue
  fi
  dest="${BUILD_DIR}/TrustAnchor/${base}"
  sed "s/\\[TODO\\]: ARGS/${OWNER_HASH}/g" "${source}" > "${dest}"
  TRUST_BUILD_SOURCES+=("${dest}")
done
if [[ ${#TRUST_BUILD_SOURCES[@]} -eq 0 ]]; then
  echo "TrustAnchor sources were filtered out unexpectedly" >&2
  exit 1
fi
"${NCCS}" -o "${BUILD_DIR}/TrustAnchor" "${TRUST_BUILD_SOURCES[@]}" >/dev/null

TRUST_HASH="$("${NEOXP}" contract hash -i "${EXPRESS_FILE}" "${BUILD_DIR}/TrustAnchor/TrustAnchor.nef" dev)"

mkdir -p "${BUILD_DIR}/TrustAnchorAgent"
sed "s/\\[TODO\\]: ARGS/${TRUST_HASH}/g" "${ROOT}/contract/TrustAnchorAgent.cs" \
  > "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.cs"
"${NCCS}" -o "${BUILD_DIR}/TrustAnchorAgent" --base-name TrustAnchorAgent "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.cs" >/dev/null

AGENT_HASH="$("${NEOXP}" contract hash -i "${EXPRESS_FILE}" "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev)"
AGENT2_HASH="$("${NEOXP}" contract hash -i "${EXPRESS_FILE}" "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev2)"

LOG_FILE="${EXPRESS_DIR}/neoxp.log"
"${NEOXP}" run -i "${EXPRESS_FILE}" -s 1 >"${LOG_FILE}" 2>&1 &
NEOXP_PID=$!
trap 'kill "${NEOXP_PID}" >/dev/null 2>&1 || true' EXIT

sleep 2

wait_tx() {
  local tx_hash=$1
  for _ in {1..10}; do
    if tx_json="$("${NEOXP}" show transaction -i "${EXPRESS_FILE}" "${tx_hash}" 2>/dev/null)"; then
      local vmstate
      vmstate="$(echo "${tx_json}" | jq -r '."application-log".executions[0].vmstate')"
      if [[ "${vmstate}" != "HALT" ]]; then
        echo "transaction ${tx_hash} faulted (${vmstate})" >&2
        echo "${tx_json}" >&2
        exit 1
      fi
      return 0
    fi
    sleep 1
  done
  echo "transaction ${tx_hash} not found" >&2
  exit 1
}

invoke_tx() {
  local file=$1
  local account=$2
  local output
  output="$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -p "${PASSWORD}" "${file}" "${account}")"
  local tx_hash
  tx_hash="$(echo "${output}" | awk '{print $3}')"
  wait_tx "${tx_hash}"
}

deploy_contract() {
  local nef=$1
  local account=$2
  local force_arg=()
  if [[ "${3:-}" == "force" ]]; then
    force_arg=(--force)
  fi
  local output
  output="$("${NEOXP}" contract deploy -i "${EXPRESS_FILE}" -p "${PASSWORD}" -j "${force_arg[@]}" "${nef}" "${account}")"
  local tx_hash
  tx_hash="$(echo "${output}" | jq -r '.txid // .transaction.hash // .hash // .tx // empty')"
  if [[ -z "${tx_hash}" || "${tx_hash}" == "null" ]]; then
    tx_hash="$(echo "${output}" | grep -Eo '0x[0-9a-fA-F]{64}' | head -n1)"
  fi
  if [[ -z "${tx_hash}" ]]; then
    echo "failed to parse deploy transaction hash" >&2
    echo "${output}" >&2
    exit 1
  fi
  wait_tx "${tx_hash}"
}

gas_dev_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 2000 GAS genesis dev)"
gas_dev_hash="$(echo "${gas_dev_output}" | awk '{print $3}')"
wait_tx "${gas_dev_hash}"

neo_dev_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 10 NEO genesis dev)"
neo_dev_hash="$(echo "${neo_dev_output}" | awk '{print $3}')"
wait_tx "${neo_dev_hash}"

gas_dev2_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 100 GAS genesis dev2)"
gas_dev2_hash="$(echo "${gas_dev2_output}" | awk '{print $3}')"
wait_tx "${gas_dev2_hash}"

sleep 2

deploy_contract "${BUILD_DIR}/TrustAnchor/TrustAnchor.nef" dev
deploy_contract "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev force
deploy_contract "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev2 force

for i in $(seq 0 20); do
  agent_hash="${AGENT_HASH}"
  if [[ "${i}" -eq 1 ]]; then
    agent_hash="${AGENT2_HASH}"
  fi
  name="agent-${i}"
  pubkey="${CANDIDATE_PUBKEYS[$i]}"
  cat > "${INVOKE_DIR}/register-agent-${i}.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "registerAgent",
  "args": [
    {"type": "Hash160", "value": "${agent_hash}"},
    {"type": "PublicKey", "value": "${pubkey}"},
    {"type": "String", "value": "${name}"}
  ]
}
EOF
  invoke_tx "${INVOKE_DIR}/register-agent-${i}.json" dev
  sleep 1
done

cat > "${INVOKE_DIR}/set-agent-voting.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "setAgentVotingById",
  "args": [
    {"type": "Integer", "value": "0"},
    {"type": "Integer", "value": "1"}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/set-agent-voting.json" dev

cat > "${INVOKE_DIR}/neo-transfer.json" <<EOF
{
  "contract": "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5",
  "operation": "transfer",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"},
    {"type": "Hash160", "value": "${TRUST_HASH}"},
    {"type": "Integer", "value": "1"},
    {"type": "String", "value": ""}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/neo-transfer.json" dev

cat > "${INVOKE_DIR}/totalstake.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "totalStake",
  "args": []
}
EOF
cat > "${INVOKE_DIR}/stakeof.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "stakeOf",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"}
  ]
}
EOF
cat > "${INVOKE_DIR}/reward.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "reward",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"}
  ]
}
EOF
cat > "${INVOKE_DIR}/claimreward.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "claimReward",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"}
  ]
}
EOF
cat > "${INVOKE_DIR}/gas-transfer.json" <<EOF
{
  "contract": "0xd2a4cff31913016155e38e474a2c06d08be276cf",
  "operation": "transfer",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"},
    {"type": "Hash160", "value": "${TRUST_HASH}"},
    {"type": "Integer", "value": "5"},
    {"type": "String", "value": ""}
  ]
}
EOF

sleep 2
total_stake=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/totalstake.json" dev | jq -r '.stack[0].value')
stake_of=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/stakeof.json" dev | jq -r '.stack[0].value')
if [[ "${total_stake}" != "1" || "${stake_of}" != "1" ]]; then
  echo "stake mismatch: total=${total_stake} stakeOf=${stake_of}" >&2
  exit 1
fi

invoke_tx "${INVOKE_DIR}/gas-transfer.json" dev
sleep 2
reward_before=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/reward.json" dev | jq -r '.stack[0].value')
if [[ "${reward_before}" -le 0 ]]; then
  echo "reward not accrued: ${reward_before}" >&2
  exit 1
fi
invoke_tx "${INVOKE_DIR}/claimreward.json" dev
sleep 2
reward_after=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/reward.json" dev | jq -r '.stack[0].value')
if [[ "${reward_after}" != "0" ]]; then
  echo "reward not cleared: ${reward_after}" >&2
  exit 1
fi

cat > "${INVOKE_DIR}/withdraw.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "withdraw",
  "args": [
    {"type": "Hash160", "value": "${OWNER_HASH}"},
    {"type": "Integer", "value": "1"}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/withdraw.json" dev

sleep 2
total_stake=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/totalstake.json" dev | jq -r '.stack[0].value')
stake_of=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/stakeof.json" dev | jq -r '.stack[0].value')
if [[ "${total_stake}" != "0" || "${stake_of}" != "0" ]]; then
  echo "withdraw mismatch: total=${total_stake} stakeOf=${stake_of}" >&2
  exit 1
fi

echo "neo-express test completed."
