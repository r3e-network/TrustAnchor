# 质押与奖励计算算法与正确性说明

本文档说明 TrustAnchor 合约中“NEO 质押 + GAS 奖励分配”的算法与正确性。目标是帮助开发者理解奖励的来源、计算公式、关键存储字段，以及为何不会多发或少发（除整数截断产生的微小舍入误差）。

相关代码位置：
- `contract/TrustAnchor/TrustAnchor.cs`（质押/赎回/NEP-17 入口）
- `contract/TrustAnchor/TrustAnchor.Rewards.cs`（奖励核算与分配）
- `contract/TrustAnchor/TrustAnchor.Constants.cs`（存储前缀与常量）

## 1. 关键存储与常量

| 名称 | 说明 | 代码常量 |
| --- | --- | --- |
| 全局 RPS | Reward Per Stake 累积值 | `PREFIXREWARDPERTOKENSTORED` |
| 用户奖励 | 用户累积的可领取 GAS | `PREFIXREWARD` |
| 用户 paid | 用户上次同步时的 RPS | `PREFIXPAID` |
| 待分配奖励 | 无质押时进入的 GAS | `PREFIXPENDINGREWARD` |
| 用户质押 | 用户质押 NEO 数量 | `PREFIXSTAKE` |
| 总质押 | 合约总质押 NEO | `PREFIXTOTALSTAKE` |

常量：
- `RPS_SCALE = 100000000`（RPS 的小数缩放，8 位）
- `DEFAULTCLAIMREMAIN = 100000000`（100% GAS 分配给质押者）

说明：RPS 的单位是“每 1 NEO 质押可得的奖励份额（带 8 位缩放）”。

## 2. 奖励分配的核心公式

当合约收到 GAS 且存在质押时，全局 RPS 增量：

```
ΔRPS = amount * DEFAULTCLAIMREMAIN / totalStake
RPS  = RPS + ΔRPS
```

当某个用户同步奖励时：

```
earned = stake * (RPS - paid) / RPS_SCALE + reward
paid   = RPS
```

其中：
- `stake` 为用户当前质押 NEO
- `paid` 为上次同步时刻的 RPS
- `reward` 为用户已累计但尚未领取的奖励

## 3. 流程说明（与合约逻辑对应）

### 3.1 GAS 进入合约（分配奖励）
入口：`OnNEP17Payment`。
- 若 `totalStake > 0`，调用 `DistributeReward(amount, totalStake)` 更新 RPS。
- 若 `totalStake == 0`，累加到 `pendingReward`，等待第一个质押出现后再分配。

### 3.2 NEO 质押
入口：`OnNEP17Payment`。
- 先 `SyncAccount(from)`，确保旧奖励入账。
- 更新用户 `stake` 与全局 `totalStake`。
- 若是首次质押（之前 `totalStake == 0`），把 `pendingReward` 立刻分配到 RPS。
- 将 NEO 转给“投票最高的代理合约”，用于代理投票（优先级逻辑不影响奖励核算）。

### 3.3 赎回（Withdraw）
入口：`Withdraw(account, amount)`。
- 先 `SyncAccount`，确保持有期奖励被结算。
- 减少用户 `stake` 与全局 `totalStake`。
- 从低投票优先级的代理合约依次取回 NEO，直到满足提现数量。

### 3.4 领取奖励（ClaimReward）
入口：`ClaimReward(account)`。
- 先 `SyncAccount`，把最新 RPS 增量计入用户。
- 把 `reward` 转出给用户，并清零。

## 4. 正确性说明（奖励守恒）

设：
- `s_i` 为第 i 个用户的质押
- `RPS` 为全局累积值
- 某次 GAS 分配使得 `ΔRPS = amount * DEFAULTCLAIMREMAIN / totalStake`

用户奖励增量：

```
Δreward_i = s_i * ΔRPS / RPS_SCALE
```

总和：

```
ΣΔreward_i = (Σ s_i) * ΔRPS / RPS_SCALE
          = totalStake * (amount * DEFAULTCLAIMREMAIN / totalStake) / RPS_SCALE
          = amount * DEFAULTCLAIMREMAIN / RPS_SCALE
```

由于 `DEFAULTCLAIMREMAIN == RPS_SCALE == 100000000`，理想数学结果为：

```
ΣΔreward_i = amount
```

这说明每次 GAS 分配在“数学理想值”下完全分配给质押者，不多发、不少发。

## 5. 整数截断与舍入误差

实际实现使用整数除法，存在向下截断：
- `ΔRPS = amount * DEFAULTCLAIMREMAIN / totalStake` 会截断；
- `stake * (RPS - paid) / RPS_SCALE` 也会截断。

因此，某些分配会产生“微小剩余 GAS”（dust），这些 dust 不会被计入任何用户奖励余额，而是停留在合约余额中。该行为是可预期且安全的，不会导致超发。

## 6. 重要边界情况

1. **无质押时收到 GAS**：进入 `pendingReward`，在第一次质押时按新的 `totalStake` 分配。
2. **用户 stake 为 0**：`SyncAccount` 仅更新 `paid`，避免未来旧 RPS 被重复计入。
3. **质押/赎回前的同步**：每次增减质押前都会 `SyncAccount`，保证奖励按持有期结算。
4. **溢出保护**：`DistributeReward` 中检查 `newRps >= rps`，防止溢出导致奖励异常。

## 7. 开发者结论

- RPS 方案保证奖励与质押比例严格对应。
- 所有奖励增量都由 GAS 进入合约触发，且不会超发。
- 若需要更精确的分配，可提高 `RPS_SCALE`（需合约升级）。

