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
OWNER_PUBKEY="$("${NEOXP}" wallet list -i "${EXPRESS_FILE}" -j | jq -r '.dev[0]."public-key"')"
DEV2_HASH="$("${NEOXP}" wallet list -i "${EXPRESS_FILE}" -j | jq -r '.dev2[0]."script-hash"')"

mkdir -p "${BUILD_DIR}/TrustAnchor"
sed "s/\\[TODO\\]: ARGS/${OWNER_HASH}/g" "${ROOT}/code/TrustAnchor.cs" > "${BUILD_DIR}/TrustAnchor/TrustAnchor.cs"
"${NCCS}" -o "${BUILD_DIR}/TrustAnchor" "${BUILD_DIR}/TrustAnchor/TrustAnchor.cs" >/dev/null

TRUST_HASH="$("${NEOXP}" contract hash -i "${EXPRESS_FILE}" "${BUILD_DIR}/TrustAnchor/TrustAnchor.nef" dev)"

mkdir -p "${BUILD_DIR}/TrustAnchorAgent"
sed "s/\\[TODO\\]: ARGS/${TRUST_HASH}/g" "${ROOT}/code/TrustAnchorAgent.cs" \
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

gas_dev_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 2000 GAS genesis dev)"
gas_dev_hash="$(echo "${gas_dev_output}" | awk '{print $3}')"
wait_tx "${gas_dev_hash}"

neo_dev_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 10 NEO genesis dev)"
neo_dev_hash="$(echo "${neo_dev_output}" | awk '{print $3}')"
wait_tx "${neo_dev_hash}"

gas_dev2_output="$("${NEOXP}" transfer -i "${EXPRESS_FILE}" -p "${PASSWORD}" 100 GAS genesis dev2)"
gas_dev2_hash="$(echo "${gas_dev2_output}" | awk '{print $3}')"
wait_tx "${gas_dev2_hash}"

register_output="$("${NEOXP}" candidate register -i "${EXPRESS_FILE}" -p "${PASSWORD}" dev)"
register_hash="$(echo "${register_output}" | awk '{print $4}')"
wait_tx "${register_hash}"

sleep 2

"${NEOXP}" contract deploy -i "${EXPRESS_FILE}" -p "${PASSWORD}" "${BUILD_DIR}/TrustAnchor/TrustAnchor.nef" dev >/dev/null
"${NEOXP}" contract deploy -i "${EXPRESS_FILE}" -p "${PASSWORD}" "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev >/dev/null
"${NEOXP}" contract deploy -i "${EXPRESS_FILE}" -p "${PASSWORD}" "${BUILD_DIR}/TrustAnchorAgent/TrustAnchorAgent.nef" dev2 >/dev/null

cat > "${INVOKE_DIR}/setagent.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "setAgent",
  "args": [
    {"type": "Integer", "value": "0"},
    {"type": "Hash160", "value": "${AGENT_HASH}"}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/setagent.json" dev
cat > "${INVOKE_DIR}/setagent1.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "setAgent",
  "args": [
    {"type": "Integer", "value": "1"},
    {"type": "Hash160", "value": "${AGENT2_HASH}"}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/setagent1.json" dev

sleep 2

cat > "${INVOKE_DIR}/allowcandidate.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "allowCandidate",
  "args": [
    {"type": "PublicKey", "value": "${OWNER_PUBKEY}"}
  ]
}
EOF
invoke_tx "${INVOKE_DIR}/allowcandidate.json" dev

sleep 2

cat > "${INVOKE_DIR}/candidate.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "candidate",
  "args": [
    {"type": "PublicKey", "value": "${OWNER_PUBKEY}"}
  ]
}
EOF

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
cat > "${INVOKE_DIR}/trigvote.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "trigVote",
  "args": [
    {"type": "Integer", "value": "0"},
    {"type": "PublicKey", "value": "${OWNER_PUBKEY}"}
  ]
}
EOF
cat > "${INVOKE_DIR}/trigtransfer.json" <<EOF
{
  "contract": "${TRUST_HASH}",
  "operation": "trigTransfer",
  "args": [
    {"type": "Integer", "value": "0"},
    {"type": "Integer", "value": "1"},
    {"type": "Integer", "value": "1"}
  ]
}
EOF
cat > "${INVOKE_DIR}/getaccountstate-agent0.json" <<EOF
{
  "contract": "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5",
  "operation": "getAccountState",
  "args": [
    {"type": "Hash160", "value": "${AGENT_HASH}"}
  ]
}
EOF
cat > "${INVOKE_DIR}/neobalance-agent1.json" <<EOF
{
  "contract": "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5",
  "operation": "balanceOf",
  "args": [
    {"type": "Hash160", "value": "${AGENT2_HASH}"}
  ]
}
EOF

sleep 2
total_stake=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/totalstake.json" dev | jq -r '.stack[0].value')
stake_of=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/stakeof.json" dev | jq -r '.stack[0].value')
if [[ "${total_stake}" != "100000000" || "${stake_of}" != "100000000" ]]; then
  echo "stake mismatch: total=${total_stake} stakeOf=${stake_of}" >&2
  exit 1
fi

candidate_type=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/candidate.json" dev | jq -r '.stack[0].type')
if [[ "${candidate_type}" != "ByteString" ]]; then
  echo "candidate not whitelisted" >&2
  exit 1
fi

invoke_tx "${INVOKE_DIR}/trigvote.json" dev
sleep 2

invoke_tx "${INVOKE_DIR}/trigtransfer.json" dev
sleep 2
agent1_balance=$("${NEOXP}" contract invoke -i "${EXPRESS_FILE}" -j -r "${INVOKE_DIR}/neobalance-agent1.json" dev | jq -r '.stack[0].value')
if [[ "${agent1_balance}" != "1" ]]; then
  echo "agent1 balance mismatch: ${agent1_balance}" >&2
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
