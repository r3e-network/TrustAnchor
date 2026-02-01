import { useState } from 'react';
import { 
  Coins, 
  ArrowDownCircle, 
  ArrowUpCircle,
  Gift,
  AlertTriangle,
  Info,
  CheckCircle2
} from 'lucide-react';
import toast from 'react-hot-toast';
import { Card, Button, Input, StatCard, ConfirmModal } from '../components';
import { formatNumber } from '../abis/TrustAnchor';
import type { StakeInfo, TransactionResult } from '../types';

// ============================================
// Types
// ============================================

interface StakingProps {
  readonly connected: boolean;
  readonly isPaused: boolean;
  readonly stakeInfo: StakeInfo;
  readonly deposit: (amount: number) => Promise<TransactionResult>;
  readonly withdraw: (amount: number) => Promise<TransactionResult>;
  readonly claimReward: () => Promise<TransactionResult>;
  readonly emergencyWithdraw: () => Promise<TransactionResult>;
}

// ============================================
// Action Card Component
// ============================================

interface ActionCardProps {
  title: string;
  description: string;
  icon: React.ReactNode;
  iconColor: string;
  children: React.ReactNode;
}

function ActionCard({ title, description, icon, iconColor, children }: ActionCardProps) {
  return (
    <Card>
      <div className="flex items-center space-x-3 mb-6">
        <div className={`p-3 rounded-xl ${iconColor}`}>
          {icon}
        </div>
        <div>
          <h2 className="text-xl font-bold text-white">{title}</h2>
          <p className="text-sm text-slate-400">{description}</p>
        </div>
      </div>
      {children}
    </Card>
  );
}

// ============================================
// Deposit Form Component
// ============================================

interface DepositFormProps {
  connected: boolean;
  isPaused: boolean;
  onDeposit: (amount: number) => Promise<TransactionResult>;
}

function DepositForm({ connected, isPaused, onDeposit }: DepositFormProps) {
  const [amount, setAmount] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async () => {
    if (!amount || parseFloat(amount) <= 0) {
      toast.error('Please enter a valid amount');
      return;
    }

    setIsLoading(true);
    const result = await onDeposit(parseFloat(amount));
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success(
        <div>
          <p className="font-semibold">Deposit submitted!</p>
          <p className="text-xs">TX: {result.txid.slice(0, 10)}...</p>
        </div>
      );
      setAmount('');
    } else {
      toast.error(result.message || 'Deposit failed');
    }
  };

  if (!connected) {
    return (
      <div className="text-center py-8 bg-slate-900/50 rounded-lg">
        <p className="text-slate-500">Connect your wallet to deposit</p>
      </div>
    );
  }

  if (isPaused) {
    return (
      <div className="flex items-center space-x-2 p-4 bg-red-500/10 border border-red-500/20 rounded-lg">
        <AlertTriangle className="w-5 h-5 text-red-400" />
        <span className="text-red-400">Deposits are currently paused</span>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Input
        type="number"
        label="Amount (NEO)"
        value={amount}
        onChange={(e) => setAmount(e.target.value)}
        placeholder="Enter amount"
        min="1"
      />
      <Button
        onClick={handleSubmit}
        isLoading={isLoading}
        leftIcon={<ArrowDownCircle className="w-5 h-5" />}
        fullWidth
      >
        Deposit NEO
      </Button>
      <p className="text-xs text-slate-500 flex items-start space-x-1">
        <Info className="w-4 h-4 flex-shrink-0 mt-0.5" />
        <span>Your NEO will be delegated to the highest priority agent to earn voting rewards.</span>
      </p>
    </div>
  );
}

// ============================================
// Withdraw Form Component
// ============================================

interface WithdrawFormProps {
  connected: boolean;
  maxAmount: string;
  onWithdraw: (amount: number) => Promise<TransactionResult>;
}

function WithdrawForm({ connected, maxAmount, onWithdraw }: WithdrawFormProps) {
  const [amount, setAmount] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleMaxClick = () => setAmount(maxAmount);

  const handleSubmit = async () => {
    if (!amount || parseFloat(amount) <= 0) {
      toast.error('Please enter a valid amount');
      return;
    }
    if (parseFloat(amount) > parseFloat(maxAmount)) {
      toast.error('Insufficient staked balance');
      return;
    }

    setIsLoading(true);
    const result = await onWithdraw(parseFloat(amount));
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success(
        <div>
          <p className="font-semibold">Withdrawal submitted!</p>
          <p className="text-xs">TX: {result.txid.slice(0, 10)}...</p>
        </div>
      );
      setAmount('');
    } else {
      toast.error(result.message || 'Withdrawal failed');
    }
  };

  if (!connected) {
    return (
      <div className="text-center py-8 bg-slate-900/50 rounded-lg">
        <p className="text-slate-500">Connect your wallet to withdraw</p>
      </div>
    );
  }

  if (parseFloat(maxAmount) <= 0) {
    return (
      <div className="text-center py-8 bg-slate-900/50 rounded-lg">
        <p className="text-slate-500">You have no staked NEO</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Input
        type="number"
        label="Amount (NEO)"
        value={amount}
        onChange={(e) => setAmount(e.target.value)}
        placeholder="Enter amount"
        min="1"
        max={maxAmount}
        rightIcon={
          <button
            onClick={handleMaxClick}
            className="text-xs text-green-400 hover:text-green-300"
          >
            Max
          </button>
        }
      />
      <Button
        variant="secondary"
        onClick={handleSubmit}
        isLoading={isLoading}
        leftIcon={<ArrowUpCircle className="w-5 h-5" />}
        fullWidth
      >
        Withdraw NEO
      </Button>
      <p className="text-xs text-slate-500 flex items-start space-x-1">
        <Info className="w-4 h-4 flex-shrink-0 mt-0.5" />
        <span>Withdrawal pulls NEO from agents starting with lowest voting priority.</span>
      </p>
    </div>
  );
}

// ============================================
// Claim Rewards Card Component
// ============================================

interface ClaimCardProps {
  connected: boolean;
  reward: string;
  onClaim: () => Promise<TransactionResult>;
}

function ClaimCard({ connected, reward, onClaim }: ClaimCardProps) {
  const [isLoading, setIsLoading] = useState(false);

  const handleClaim = async () => {
    if (parseFloat(reward) <= 0) {
      toast.error('No rewards to claim');
      return;
    }

    setIsLoading(true);
    const result = await onClaim();
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success(
        <div>
          <p className="font-semibold">Rewards claimed!</p>
          <p className="text-xs">TX: {result.txid.slice(0, 10)}...</p>
        </div>
      );
    } else {
      toast.error(result.message || 'Claim failed');
    }
  };

  return (
    <Card>
      <div className="flex flex-col md:flex-row items-center justify-between space-y-4 md:space-y-0">
        <div className="flex items-center space-x-4">
          <div className="p-4 bg-yellow-500/20 rounded-xl">
            <Gift className="w-8 h-8 text-yellow-400" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white">Claim Rewards</h2>
            <p className="text-slate-400">
              {connected 
                ? `You have ${formatNumber(reward)} GAS available to claim`
                : 'Connect wallet to view rewards'}
            </p>
          </div>
        </div>
        <Button
          onClick={handleClaim}
          disabled={!connected || parseFloat(reward) <= 0}
          isLoading={isLoading}
          leftIcon={<Gift className="w-5 h-5" />}
        >
          Claim GAS
        </Button>
      </div>
    </Card>
  );
}

// ============================================
// Emergency Withdraw Section Component
// ============================================

interface EmergencySectionProps {
  connected: boolean;
  stake: string;
  onEmergencyWithdraw: () => Promise<TransactionResult>;
}

function EmergencySection({ connected, stake, onEmergencyWithdraw }: EmergencySectionProps) {
  const [showModal, setShowModal] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleConfirm = async () => {
    setIsLoading(true);
    const result = await onEmergencyWithdraw();
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success(
        <div>
          <p className="font-semibold">Emergency withdrawal submitted!</p>
          <p className="text-xs">TX: {result.txid.slice(0, 10)}...</p>
        </div>
      );
      setShowModal(false);
    } else {
      toast.error(result.message || 'Emergency withdrawal failed');
    }
  };

  return (
    <>
      <div className="border border-red-500/30 bg-red-500/5 rounded-xl p-6">
        <div className="flex items-start space-x-4">
          <AlertTriangle className="w-6 h-6 text-red-400 flex-shrink-0 mt-1" />
          <div className="flex-1">
            <h3 className="text-lg font-semibold text-red-400 mb-2">Emergency Withdrawal</h3>
            <p className="text-slate-400 text-sm mb-4">
              Only available when the contract is paused and all agents have zero NEO balance. 
              This is a safety mechanism for extreme scenarios. Use with caution.
            </p>
            <Button
              variant="danger"
              size="sm"
              onClick={() => setShowModal(true)}
              disabled={!connected}
            >
              Emergency Withdraw
            </Button>
          </div>
        </div>
      </div>

      <ConfirmModal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        onConfirm={handleConfirm}
        title="Confirm Emergency Withdrawal"
        message={`You are about to execute an emergency withdrawal of ${formatNumber(stake)} NEO. This operation is irreversible.`}
        confirmText="Confirm Withdraw"
        isLoading={isLoading}
        variant="danger"
      />
    </>
  );
}

// ============================================
// Main Staking Component
// ============================================

export function Staking({
  connected,
  isPaused,
  stakeInfo,
  deposit,
  withdraw,
  claimReward,
  emergencyWithdraw,
}: StakingProps) {
  const poolShare = parseFloat(stakeInfo.totalStake) > 0 
    ? ((parseFloat(stakeInfo.stake) / parseFloat(stakeInfo.totalStake)) * 100).toFixed(4)
    : '0';

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="text-center max-w-2xl mx-auto">
        <h1 className="text-3xl font-bold text-white mb-4">Staking</h1>
        <p className="text-slate-400">
          Stake your NEO to earn GAS rewards. 100% of rewards are distributed to stakers 
          proportionally—no platform fees.
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-4xl mx-auto">
        <StatCard
          title="Your Staked"
          value={connected ? formatNumber(stakeInfo.stake) : '-'}
          subtitle="NEO"
          icon={<Coins className="w-8 h-8" />}
          iconColor="green"
        />
        <StatCard
          title="Claimable Rewards"
          value={connected ? formatNumber(stakeInfo.reward) : '-'}
          subtitle="GAS"
          icon={<Gift className="w-8 h-8" />}
          iconColor="yellow"
        />
        <StatCard
          title="Pool Share"
          value={connected ? `${poolShare}%` : '-'}
          subtitle="of total staked"
          icon={<CheckCircle2 className="w-8 h-8" />}
          iconColor="blue"
        />
      </div>

      {/* Action Cards */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 max-w-5xl mx-auto">
        <ActionCard
          title="Deposit NEO"
          description="Stake NEO to start earning rewards"
          icon={<ArrowDownCircle className="w-6 h-6 text-green-400" />}
          iconColor="bg-green-500/20"
        >
          <DepositForm
            connected={connected}
            isPaused={isPaused}
            onDeposit={deposit}
          />
        </ActionCard>

        <ActionCard
          title="Withdraw NEO"
          description="Unstake your NEO"
          icon={<ArrowUpCircle className="w-6 h-6 text-blue-400" />}
          iconColor="bg-blue-500/20"
        >
          <WithdrawForm
            connected={connected}
            maxAmount={stakeInfo.stake}
            onWithdraw={withdraw}
          />
        </ActionCard>
      </div>

      {/* Claim Rewards */}
      <div className="max-w-2xl mx-auto">
        <ClaimCard
          connected={connected}
          reward={stakeInfo.reward}
          onClaim={claimReward}
        />
      </div>

      {/* Emergency Withdraw */}
      <div className="max-w-2xl mx-auto">
        <EmergencySection
          connected={connected}
          stake={stakeInfo.stake}
          onEmergencyWithdraw={emergencyWithdraw}
        />
      </div>
    </div>
  );
}

export default Staking;
