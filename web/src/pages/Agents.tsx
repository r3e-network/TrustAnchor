import { useState, useMemo } from 'react';
import { 
  Users, 
  Target, 
  Vote, 
  Hash,
  TrendingUp,
  Search,
  AlertCircle
} from 'lucide-react';
import { Card, Badge, EmptyState, Button, LoadingSpinner, Input } from '../components';
import { shortenHash, CONSTANTS } from '../abis/TrustAnchor';
import type { Agent, TransactionResult } from '../types';

// ============================================
// Types
// ============================================

interface AgentsProps {
  readonly agents: Agent[];
  readonly isOwner: boolean;
  readonly loading: boolean;
  readonly voteAgent: (index: number) => Promise<TransactionResult>;
}

// ============================================
// Info Card Component
// ============================================

interface InfoCardProps {
  title: string;
  description: string;
  icon: React.ReactNode;
  iconColor: string;
}

function InfoCard({ title, description, icon, iconColor }: InfoCardProps) {
  return (
    <Card>
      <div className="flex items-center space-x-3 mb-3">
        <div className={`p-2 rounded-lg ${iconColor}`}>
          {icon}
        </div>
        <h3 className="font-semibold text-white">{title}</h3>
      </div>
      <p className="text-sm text-slate-400">{description}</p>
    </Card>
  );
}

// ============================================
// Agent Status Badge Component
// ============================================

function getAgentStatus(voting: string): { label: string; variant: 'success' | 'warning' | 'info' | 'neutral' } {
  const votingNum = parseInt(voting, 10);
  if (votingNum >= 100) return { label: 'High Priority', variant: 'success' };
  if (votingNum >= 50) return { label: 'Medium Priority', variant: 'warning' };
  if (votingNum > 0) return { label: 'Low Priority', variant: 'info' };
  return { label: 'Inactive', variant: 'neutral' };
}

// ============================================
// Agent Card Component
// ============================================

interface AgentCardProps {
  agent: Agent;
  isOwner: boolean;
  onVote: (index: number) => Promise<TransactionResult>;
}

function AgentCard({ agent, isOwner, onVote }: AgentCardProps) {
  const [isVoting, setIsVoting] = useState(false);
  const status = getAgentStatus(agent.voting);

  const handleVote = async () => {
    setIsVoting(true);
    await onVote(agent.index);
    setIsVoting(false);
  };

  return (
    <Card variant="hover">
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        {/* Agent Info */}
        <div className="flex items-start space-x-4">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-slate-700 to-slate-800 flex items-center justify-center flex-shrink-0">
            <span className="text-lg font-bold text-slate-400">#{agent.index}</span>
          </div>
          
          <div className="flex-1 min-w-0">
            <div className="flex items-center space-x-3 mb-1">
              <h3 className="text-lg font-semibold text-white truncate">{agent.name}</h3>
              <Badge variant={status.variant} size="sm">{status.label}</Badge>
            </div>
            
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-sm">
              <div className="flex items-center space-x-2">
                <Hash className="w-4 h-4 text-slate-500" />
                <span className="text-slate-400 font-mono">
                  {shortenHash(agent.address, 10, 8)}
                </span>
              </div>
              <div className="flex items-center space-x-2">
                <Target className="w-4 h-4 text-slate-500" />
                <span className="text-slate-400 font-mono">
                  {shortenHash(agent.target, 10, 8)}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center space-x-6">
          <div className="text-right">
            <div className="text-sm text-slate-500">Voting Priority</div>
            <div className="text-xl font-bold text-white">{agent.voting}</div>
          </div>
          
          {isOwner && (
            <Button
              variant="secondary"
              size="sm"
              onClick={handleVote}
              isLoading={isVoting}
              leftIcon={<Vote className="w-4 h-4" />}
            >
              Vote
            </Button>
          )}
        </div>
      </div>

      {/* Expanded Details */}
      <div className="mt-4 pt-4 border-t border-slate-700/50 opacity-0 group-hover:opacity-100 transition-opacity hidden lg:block">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-slate-500">Agent Contract:</span>
            <span className="ml-2 text-green-400 font-mono break-all">{agent.address}</span>
          </div>
          <div>
            <span className="text-slate-500">Voting Target:</span>
            <span className="ml-2 text-slate-300 font-mono break-all">{agent.target}</span>
          </div>
        </div>
      </div>
    </Card>
  );
}

// ============================================
// How It Works Section Component
// ============================================

const WORKFLOW_STEPS = [
  {
    step: '1',
    title: 'NEO Deposited',
    desc: 'Users stake NEO into TrustAnchor',
  },
  {
    step: '2',
    title: 'Auto-Routing',
    desc: 'NEO is routed to highest priority agent',
  },
  {
    step: '3',
    title: 'Voting',
    desc: 'Agents vote for their target candidates',
  },
  {
    step: '4',
    title: 'Rewards',
    desc: 'GAS rewards distributed to stakers',
  },
];

function HowItWorksSection() {
  return (
    <Card>
      <h2 className="text-xl font-bold text-white mb-6">How Agent Voting Works</h2>
      
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        {WORKFLOW_STEPS.map((item, i) => (
          <div key={i} className="relative">
            <div className="flex items-center space-x-3 mb-2">
              <div className="w-8 h-8 rounded-full bg-green-500/20 flex items-center justify-center">
                <span className="text-sm font-bold text-green-400">{item.step}</span>
              </div>
              <h3 className="font-semibold text-white">{item.title}</h3>
            </div>
            <p className="text-sm text-slate-400 pl-11">{item.desc}</p>
            
            {i < WORKFLOW_STEPS.length - 1 && (
              <div className="hidden md:block absolute top-4 left-full w-full h-px bg-gradient-to-r from-green-500/50 to-transparent" />
            )}
          </div>
        ))}
      </div>
    </Card>
  );
}

// ============================================
// Main Agents Component
// ============================================

export function Agents({ agents, isOwner, loading, voteAgent }: AgentsProps) {
  const [searchTerm, setSearchTerm] = useState('');

  const filteredAgents = useMemo(() => {
    if (!searchTerm) return agents;
    const term = searchTerm.toLowerCase();
    return agents.filter(agent => 
      agent.name.toLowerCase().includes(term) ||
      agent.address.toLowerCase().includes(term) ||
      agent.target.toLowerCase().includes(term)
    );
  }, [agents, searchTerm]);

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">Agents</h1>
          <p className="text-slate-400">
            TrustAnchor uses up to {CONSTANTS.MAX_AGENTS} agents to delegate voting power 
            to active contributors and reputable community members.
          </p>
        </div>
        <div className="flex items-center space-x-2 text-sm">
          <span className="text-slate-500">Total Agents:</span>
          <span className="font-bold text-white">{agents.length}</span>
          <span className="text-slate-500">/ {CONSTANTS.MAX_AGENTS}</span>
        </div>
      </div>

      {/* Info Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <InfoCard
          title="Voting Delegation"
          description="Each agent votes for a specific candidate in the NEO consensus. Higher voting priority means more NEO deposits are routed to that agent."
          icon={<Vote className="w-5 h-5 text-green-400" />}
          iconColor="bg-green-500/20"
        />
        <InfoCard
          title="Reward Distribution"
          description="All GAS rewards earned from voting are distributed to stakers proportionally based on their staked NEO amount."
          icon={<TrendingUp className="w-5 h-5 text-blue-400" />}
          iconColor="bg-blue-500/20"
        />
        <InfoCard
          title="Target Candidates"
          description="Agents vote for active contributors, developers, researchers, and community members with proven track records."
          icon={<Target className="w-5 h-5 text-purple-400" />}
          iconColor="bg-purple-500/20"
        />
      </div>

      {/* Search */}
      <div className="relative max-w-md">
        <Input
          label=""
          placeholder="Search agents by name, address, or target..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          leftIcon={<Search className="w-5 h-5" />}
        />
      </div>

      {/* Agents List */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <LoadingSpinner size="lg" />
        </div>
      ) : agents.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Users className="w-16 h-16" />}
            title="No Agents Registered"
            description="There are currently no agents registered in the TrustAnchor contract. Agents will appear here once they are registered by the contract owner."
          />
        </Card>
      ) : filteredAgents.length === 0 ? (
        <Card>
          <EmptyState
            icon={<AlertCircle className="w-16 h-16" />}
            title="No Results Found"
            description="No agents match your search criteria. Try a different search term."
          />
        </Card>
      ) : (
        <div className="space-y-4">
          {filteredAgents.map((agent) => (
            <AgentCard
              key={agent.index}
              agent={agent}
              isOwner={isOwner}
              onVote={voteAgent}
            />
          ))}
        </div>
      )}

      {/* How It Works */}
      <HowItWorksSection />
    </div>
  );
}

export default Agents;
