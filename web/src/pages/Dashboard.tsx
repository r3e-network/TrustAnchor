import { useEffect } from "react";
import { Link } from "react-router-dom";
import { Wallet, Users, TrendingUp, ArrowRight, Shield, PauseCircle, PlayCircle } from "lucide-react";
import { Card, StatCard, Badge, EmptyState } from "../components";
import { formatNumber, shortenHash } from "../abis/TrustAnchor";
import { NETWORKS } from "../types";
import type { Agent, StakeInfo, NetworkType } from "../types";

// ============================================
// Types
// ============================================

interface DashboardProps {
  readonly connected: boolean;
  readonly address: string | null;
  readonly contractHash: string;
  readonly owner: string;
  readonly isPaused: boolean;
  readonly agents: Agent[];
  readonly stakeInfo: StakeInfo;
  readonly loading: boolean;
  readonly isOwner: boolean;
  readonly network: NetworkType;
  readonly fetchContractState: () => Promise<void>;
  readonly fetchUserStakeInfo: (address: string) => Promise<void>;
}

// ============================================
// Hero Section Component
// ============================================

interface HeroSectionProps {
  isPaused: boolean;
}

function HeroSection({ isPaused }: HeroSectionProps) {
  return (
    <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-slate-800 to-slate-900 border border-slate-700/50 p-8">
      {/* Background decoration */}
      <div className="absolute top-0 right-0 w-64 h-64 bg-green-500/10 rounded-full blur-3xl -translate-y-1/2 translate-x-1/2" />

      <div className="relative z-10">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-green-500/20 rounded-lg">
              <Shield className="w-6 h-6 text-green-400" />
            </div>
            <h1 className="text-3xl font-bold text-white">TrustAnchor Dashboard</h1>
          </div>

          {isPaused ? (
            <Badge variant="error" icon={<PauseCircle className="w-3 h-3" />}>
              Paused
            </Badge>
          ) : (
            <Badge variant="success" icon={<PlayCircle className="w-3 h-3" />}>
              Active
            </Badge>
          )}
        </div>

        <p className="text-slate-400 max-w-2xl mb-6">
          Non-profit decentralized NEO voting delegation system. 100% of GAS rewards are distributed to stakers based on
          their staked amount—no fees, no profit.
        </p>

        <div className="flex flex-wrap gap-4">
          <Link
            to="/staking"
            className="inline-flex items-center space-x-2 px-6 py-3 bg-green-500 hover:bg-green-600 text-white font-semibold rounded-lg transition-colors"
          >
            <TrendingUp className="w-5 h-5" />
            <span>Start Staking</span>
            <ArrowRight className="w-4 h-4" />
          </Link>
          <Link
            to="/agents"
            className="inline-flex items-center space-x-2 px-6 py-3 bg-slate-700 hover:bg-slate-600 text-white font-semibold rounded-lg transition-colors"
          >
            <Users className="w-5 h-5" />
            <span>View Agents</span>
          </Link>
        </div>
      </div>
    </div>
  );
}

// ============================================
// Stats Grid Component
// ============================================

interface StatsGridProps {
  totalStake: string;
  agentCount: number;
  userStake: string;
  userReward: string;
  loading: boolean;
  connected: boolean;
}

function StatsGrid({ totalStake, agentCount, userStake, userReward, loading, connected }: StatsGridProps) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <StatCard
        title="Total Staked"
        value={loading ? "..." : formatNumber(totalStake)}
        subtitle="NEO"
        icon={<Wallet className="w-6 h-6" />}
        iconColor="blue"
        isLoading={loading}
      />
      <StatCard
        title="Active Agents"
        value={loading ? "..." : agentCount}
        subtitle="of 21 max"
        icon={<Users className="w-6 h-6" />}
        iconColor="purple"
        isLoading={loading}
      />
      <StatCard
        title="Your Stake"
        value={connected ? formatNumber(userStake) : "-"}
        subtitle="NEO"
        icon={<TrendingUp className="w-6 h-6" />}
        iconColor="green"
        isLoading={loading && connected}
      />
      <StatCard
        title="Your Rewards"
        value={connected ? formatNumber(userReward) : "-"}
        subtitle="GAS"
        icon={<TrendingUp className="w-6 h-6" />}
        iconColor="yellow"
        isLoading={loading && connected}
      />
    </div>
  );
}

// ============================================
// Contract Info Card Component
// ============================================

interface ContractInfoCardProps {
  network: NetworkType;
  contractHash: string;
  owner: string;
  isPaused: boolean;
  isOwner: boolean;
}

function ContractInfoCard({ network, contractHash, owner, isPaused, isOwner }: ContractInfoCardProps) {
  const explorerUrl = NETWORKS[network].blockExplorer;

  return (
    <Card>
      <div className="flex items-center space-x-3 mb-6">
        <div className="p-2 bg-slate-700/50 rounded-lg">
          <Shield className="w-5 h-5 text-green-400" />
        </div>
        <h2 className="text-xl font-bold text-white">Contract Information</h2>
      </div>

      <div className="space-y-4">
        <InfoRow label="Network" value={network.charAt(0).toUpperCase() + network.slice(1)} />
        <InfoRow
          label="Contract Hash"
          value={shortenHash(contractHash)}
          link={`${explorerUrl}/contract/${contractHash.replace("0x", "")}`}
        />
        <InfoRow
          label="Owner"
          value={shortenHash(owner)}
          badge={isOwner ? { text: "You", variant: "purple" } : undefined}
        />
        <InfoRow
          label="Status"
          value={isPaused ? "Paused" : "Active"}
          valueColor={isPaused ? "text-red-400" : "text-green-400"}
        />
      </div>
    </Card>
  );
}

// ============================================
// User Position Card Component
// ============================================

interface UserPositionCardProps {
  connected: boolean;
  address: string | null;
  stake: string;
  reward: string;
  totalStake: string;
}

function UserPositionCard({ connected, address, stake, reward, totalStake }: UserPositionCardProps) {
  const poolShare = parseFloat(totalStake) > 0 ? ((parseFloat(stake) / parseFloat(totalStake)) * 100).toFixed(4) : "0";

  return (
    <Card>
      <div className="flex items-center space-x-3 mb-6">
        <div className="p-2 bg-slate-700/50 rounded-lg">
          <Wallet className="w-5 h-5 text-blue-400" />
        </div>
        <h2 className="text-xl font-bold text-white">Your Position</h2>
      </div>

      {!connected ? (
        <EmptyState
          icon={<Wallet className="w-12 h-12" />}
          title="Wallet Not Connected"
          description="Connect your wallet to view your staking position"
        />
      ) : (
        <div className="space-y-4">
          <InfoRow label="Connected Address" value={shortenHash(address ?? "")} />
          <InfoRow label="Your Stake" value={`${formatNumber(stake)} NEO`} />
          <InfoRow label="Share of Pool" value={`${poolShare}%`} />
          <InfoRow label="Claimable Rewards" value={`${formatNumber(reward)} GAS`} valueColor="text-green-400" />
        </div>
      )}
    </Card>
  );
}

// ============================================
// Agents Preview Component
// ============================================

interface AgentsPreviewProps {
  agents: Agent[];
}

function AgentsPreview({ agents }: AgentsPreviewProps) {
  return (
    <Card>
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-slate-700/50 rounded-lg">
            <Users className="w-5 h-5 text-purple-400" />
          </div>
          <h2 className="text-xl font-bold text-white">Active Agents</h2>
        </div>
        <Link
          to="/agents"
          className="text-green-400 hover:text-green-300 text-sm font-medium flex items-center space-x-1"
        >
          <span>View All</span>
          <ArrowRight className="w-4 h-4" />
        </Link>
      </div>

      {agents.length === 0 ? (
        <EmptyState
          title="No Agents Registered"
          description="Agents will appear here once they are registered by the contract owner."
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {agents.slice(0, 6).map((agent) => (
            <AgentCard key={agent.index} agent={agent} />
          ))}
        </div>
      )}
    </Card>
  );
}

// ============================================
// Agent Card Component
// ============================================

function AgentCard({ agent }: { agent: Agent }) {
  return (
    <div className="bg-slate-900/50 rounded-lg p-4 border border-slate-700/50 hover:border-slate-600 transition-colors">
      <div className="flex items-center justify-between mb-2">
        <span className="font-semibold text-white">{agent.name}</span>
        <span className="text-xs text-slate-500">#{agent.index}</span>
      </div>
      <div className="text-xs text-slate-400 font-mono mb-2">{shortenHash(agent.address, 8, 6)}</div>
      <div className="flex items-center justify-between text-sm">
        <span className="text-slate-500">Priority:</span>
        <span className="text-green-400 font-medium">{agent.voting}</span>
      </div>
    </div>
  );
}

// ============================================
// Info Row Component
// ============================================

interface InfoRowProps {
  label: string;
  value: string;
  link?: string;
  badge?: { text: string; variant: "purple" | "success" | "info" };
  valueColor?: string;
}

function InfoRow({ label, value, link, badge, valueColor = "text-white" }: InfoRowProps) {
  return (
    <div className="flex items-center justify-between py-3 border-b border-slate-700/50 last:border-0">
      <span className="text-slate-400">{label}</span>
      <div className="flex items-center space-x-2">
        {link ? (
          <a
            href={link}
            target="_blank"
            rel="noopener noreferrer"
            className="font-mono text-sm text-green-400 hover:text-green-300"
          >
            {value}
          </a>
        ) : (
          <span className={`font-medium ${valueColor}`}>{value}</span>
        )}
        {badge && (
          <Badge variant={badge.variant} size="sm">
            {badge.text}
          </Badge>
        )}
      </div>
    </div>
  );
}

// ============================================
// Main Dashboard Component
// ============================================

export function Dashboard({
  connected,
  address,
  contractHash,
  owner,
  isPaused,
  agents,
  stakeInfo,
  loading,
  isOwner,
  network,
  fetchContractState,
  fetchUserStakeInfo,
}: DashboardProps) {
  // Auto-refresh every 30 seconds
  useEffect(() => {
    const interval = setInterval(() => {
      fetchContractState();
      if (connected && address) {
        fetchUserStakeInfo(address);
      }
    }, 30000);

    return () => clearInterval(interval);
  }, [fetchContractState, fetchUserStakeInfo, connected, address]);

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Hero Section */}
      <HeroSection isPaused={isPaused} />

      {/* Stats Grid */}
      <StatsGrid
        totalStake={stakeInfo.totalStake}
        agentCount={agents.length}
        userStake={stakeInfo.stake}
        userReward={stakeInfo.reward}
        loading={loading}
        connected={connected}
      />

      {/* Two Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ContractInfoCard
          network={network}
          contractHash={contractHash}
          owner={owner}
          isPaused={isPaused}
          isOwner={isOwner}
        />
        <UserPositionCard
          connected={connected}
          address={address}
          stake={stakeInfo.stake}
          reward={stakeInfo.reward}
          totalStake={stakeInfo.totalStake}
        />
      </div>

      {/* Agents Preview */}
      <AgentsPreview agents={agents} />
    </div>
  );
}

export default Dashboard;
