import { useState, useCallback } from 'react';
import { 
  Shield, 
  UserPlus, 
  Settings, 
  PauseCircle, 
  PlayCircle,
  Key,
  Vote,
  AlertTriangle,
  CheckCircle,
  RefreshCw,
  Info
} from 'lucide-react';
import toast from 'react-hot-toast';
import { Card, Button, Input, Badge, EmptyState, ConfirmModal } from '../components';
import { isValidPublicKey, isValidScriptHash, shortenHash, CONSTANTS } from '../abis/TrustAnchor';
import type { Agent, TransactionResult, TabId } from '../types';

// ============================================
// Types
// ============================================

interface AdminProps {
  readonly connected: boolean;
  readonly address: string | null;
  readonly isOwner: boolean;
  readonly isPaused: boolean;
  readonly agents: Agent[];
  readonly owner: string;
  readonly registerAgent: (agentHash: string, target: string, name: string) => Promise<TransactionResult>;
  readonly updateAgentTarget: (index: number, target: string) => Promise<TransactionResult>;
  readonly updateAgentName: (index: number, name: string) => Promise<TransactionResult>;
  readonly setAgentVoting: (index: number, amount: number) => Promise<TransactionResult>;
  readonly voteAgent: (index: number) => Promise<TransactionResult>;
  readonly pause: () => Promise<TransactionResult>;
  readonly unpause: () => Promise<TransactionResult>;
  readonly transferOwner: (newOwner: string) => Promise<TransactionResult>;
  readonly fetchContractState: () => Promise<void>;
}

// ============================================
// Tab Configuration
// ============================================

const TABS: { id: TabId; label: string; icon: typeof Settings }[] = [
  { id: 'agents', label: 'Agents', icon: UserPlus },
  { id: 'voting', label: 'Voting Config', icon: Vote },
  { id: 'contract', label: 'Contract', icon: Settings },
];

// ============================================
// Validation Utilities
// ============================================

interface ValidationResult {
  isValid: boolean;
  error?: string;
}

function validateAgentForm(data: { agentHash: string; target: string; name: string }): ValidationResult {
  if (!data.agentHash || !isValidScriptHash(data.agentHash)) {
    return { isValid: false, error: 'Invalid agent contract hash' };
  }
  if (!data.target || !isValidPublicKey(data.target)) {
    return { isValid: false, error: 'Invalid public key format' };
  }
  if (!data.name || data.name.trim().length === 0) {
    return { isValid: false, error: 'Name is required' };
  }
  if (data.name.length > CONSTANTS.MAX_AGENT_NAME_LENGTH) {
    return { isValid: false, error: `Name must be ${CONSTANTS.MAX_AGENT_NAME_LENGTH} characters or less` };
  }
  return { isValid: true };
}

// ============================================
// Register Agent Form Component
// ============================================

interface RegisterAgentFormProps {
  onRegister: (hash: string, target: string, name: string) => Promise<TransactionResult>;
  onSuccess: () => void;
}

function RegisterAgentForm({ onRegister, onSuccess }: RegisterAgentFormProps) {
  const [formData, setFormData] = useState({ agentHash: '', target: '', name: '' });
  const [isLoading, setIsLoading] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleSubmit = async () => {
    setValidationError(null);
    
    const validation = validateAgentForm(formData);
    if (!validation.isValid) {
      setValidationError(validation.error || 'Invalid form data');
      return;
    }

    setIsLoading(true);
    const result = await onRegister(formData.agentHash, formData.target, formData.name);
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success('Agent registered successfully!');
      setFormData({ agentHash: '', target: '', name: '' });
      onSuccess();
    } else {
      toast.error(result.message || 'Registration failed');
    }
  };

  return (
    <div className="space-y-4">
      <Input
        label="Agent Contract Hash"
        value={formData.agentHash}
        onChange={(e) => setFormData({ ...formData, agentHash: e.target.value })}
        placeholder="0x..."
        error={validationError?.includes('hash') ? validationError : undefined}
      />

      <Input
        label="Voting Target (Public Key)"
        value={formData.target}
        onChange={(e) => setFormData({ ...formData, target: e.target.value })}
        placeholder="02... or 03..."
        error={validationError?.includes('public key') ? validationError : undefined}
      />

      <Input
        label={`Agent Name (max ${CONSTANTS.MAX_AGENT_NAME_LENGTH} chars)`}
        value={formData.name}
        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
        placeholder="e.g., Community Agent 1"
        maxLength={CONSTANTS.MAX_AGENT_NAME_LENGTH}
        error={validationError?.includes('Name') ? validationError : undefined}
      />

      <div className="flex items-start space-x-2 p-3 bg-blue-500/10 rounded-lg">
        <Info className="w-4 h-4 text-blue-400 flex-shrink-0 mt-0.5" />
        <p className="text-xs text-blue-400">
          Maximum {CONSTANTS.MAX_AGENTS} agents allowed. Each agent needs a unique name and target.
        </p>
      </div>

      <Button
        onClick={handleSubmit}
        isLoading={isLoading}
        fullWidth
      >
        Register Agent
      </Button>
    </div>
  );
}

// ============================================
// Update Agent Form Component
// ============================================

interface UpdateAgentFormProps {
  agents: Agent[];
  onUpdateTarget: (index: number, target: string) => Promise<TransactionResult>;
  onUpdateName: (index: number, name: string) => Promise<TransactionResult>;
  onSuccess: () => void;
}

function UpdateAgentForm({ agents, onUpdateTarget, onUpdateName, onSuccess }: UpdateAgentFormProps) {
  const [selectedIndex, setSelectedIndex] = useState('');
  const [target, setTarget] = useState('');
  const [name, setName] = useState('');
  const [loading, setLoading] = useState<'target' | 'name' | null>(null);

  const handleUpdateTarget = async () => {
    if (!selectedIndex || !target) return;
    if (!isValidPublicKey(target)) {
      toast.error('Invalid public key format');
      return;
    }

    setLoading('target');
    const result = await onUpdateTarget(parseInt(selectedIndex), target);
    setLoading(null);

    if (result.status !== 'error') {
      toast.success('Agent target updated!');
      setTarget('');
      onSuccess();
    } else {
      toast.error(result.message || 'Update failed');
    }
  };

  const handleUpdateName = async () => {
    if (!selectedIndex || !name) return;
    if (name.length > CONSTANTS.MAX_AGENT_NAME_LENGTH) {
      toast.error(`Name must be ${CONSTANTS.MAX_AGENT_NAME_LENGTH} characters or less`);
      return;
    }

    setLoading('name');
    const result = await onUpdateName(parseInt(selectedIndex), name);
    setLoading(null);

    if (result.status !== 'error') {
      toast.success('Agent name updated!');
      setName('');
      onSuccess();
    } else {
      toast.error(result.message || 'Update failed');
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-slate-400 mb-2">Select Agent</label>
        <select
          value={selectedIndex}
          onChange={(e) => setSelectedIndex(e.target.value)}
          className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:ring-2 focus:ring-green-500/50 focus:border-green-500/50"
        >
          <option value="">Choose an agent...</option>
          {agents.map((agent) => (
            <option key={agent.index} value={agent.index}>
              #{agent.index} - {agent.name}
            </option>
          ))}
        </select>
      </div>

      <div className="border-t border-slate-700/50 pt-4">
        <Input
          label="Update Target"
          value={target}
          onChange={(e) => setTarget(e.target.value)}
          placeholder="New public key"
          rightIcon={
            <Button
              variant="secondary"
              size="sm"
              onClick={handleUpdateTarget}
              isLoading={loading === 'target'}
              disabled={!selectedIndex}
            >
              Update
            </Button>
          }
        />
      </div>

      <div className="border-t border-slate-700/50 pt-4">
        <Input
          label="Update Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="New name"
          maxLength={CONSTANTS.MAX_AGENT_NAME_LENGTH}
          rightIcon={
            <Button
              variant="secondary"
              size="sm"
              onClick={handleUpdateName}
              isLoading={loading === 'name'}
              disabled={!selectedIndex}
            >
              Update
            </Button>
          }
        />
      </div>
    </div>
  );
}

// ============================================
// Voting Config Form Component
// ============================================

interface VotingConfigFormProps {
  agents: Agent[];
  onSetVoting: (index: number, amount: number) => Promise<TransactionResult>;
  onVote: (index: number) => Promise<TransactionResult>;
  onSuccess: () => void;
}

function VotingConfigForm({ agents, onSetVoting, onVote, onSuccess }: VotingConfigFormProps) {
  const [selectedIndex, setSelectedIndex] = useState('');
  const [votingAmount, setVotingAmount] = useState('');
  const [loading, setLoading] = useState<'set' | 'vote' | null>(null);

  const selectedAgent = agents.find(a => a.index === parseInt(selectedIndex));

  const handleSetVoting = async () => {
    if (!selectedIndex || !votingAmount) return;

    setLoading('set');
    const result = await onSetVoting(parseInt(selectedIndex), parseInt(votingAmount));
    setLoading(null);

    if (result.status !== 'error') {
      toast.success('Voting priority updated!');
      setVotingAmount('');
      onSuccess();
    } else {
      toast.error(result.message || 'Update failed');
    }
  };

  const handleVote = async () => {
    if (!selectedIndex) return;

    setLoading('vote');
    const result = await onVote(parseInt(selectedIndex));
    setLoading(null);

    if (result.status !== 'error') {
      toast.success('Vote cast successfully!');
    } else {
      toast.error(result.message || 'Vote failed');
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <label className="block text-sm font-medium text-slate-400 mb-2">Select Agent</label>
        <select
          value={selectedIndex}
          onChange={(e) => setSelectedIndex(e.target.value)}
          className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:ring-2 focus:ring-green-500/50 focus:border-green-500/50"
        >
          <option value="">Choose an agent...</option>
          {agents.map((agent) => (
            <option key={agent.index} value={agent.index}>
              #{agent.index} - {agent.name} (current: {agent.voting})
            </option>
          ))}
        </select>
      </div>

      {selectedAgent && (
        <div className="p-4 bg-slate-900/50 rounded-lg">
          <p className="text-sm text-slate-400 mb-1">Current voting target:</p>
          <p className="font-mono text-sm text-white break-all">{selectedAgent.target}</p>
        </div>
      )}

      <div>
        <Input
          type="number"
          label="Voting Amount (Priority)"
          value={votingAmount}
          onChange={(e) => setVotingAmount(e.target.value)}
          placeholder="Enter priority amount"
          min="0"
          helperText="Higher values receive more NEO deposits. Use 0 to disable an agent."
        />
        <Button
          onClick={handleSetVoting}
          isLoading={loading === 'set'}
          disabled={!selectedIndex}
          fullWidth
          className="mt-3"
        >
          Set Voting Priority
        </Button>
      </div>

      <div className="border-t border-slate-700/50 pt-6">
        <Button
          variant="secondary"
          onClick={handleVote}
          isLoading={loading === 'vote'}
          disabled={!selectedIndex}
          leftIcon={<Vote className="w-4 h-4" />}
          fullWidth
        >
          Cast Vote
        </Button>
        <p className="text-xs text-slate-500 mt-2 text-center">
          Triggers the agent to vote for its configured target candidate on the NEO network.
        </p>
      </div>
    </div>
  );
}

// ============================================
// Contract Controls Component
// ============================================

interface ContractControlsProps {
  isPaused: boolean;
  onPause: () => Promise<TransactionResult>;
  onUnpause: () => Promise<TransactionResult>;
  onRefresh: () => Promise<void>;
}

function ContractControls({ isPaused, onPause, onUnpause, onRefresh }: ContractControlsProps) {
  const [isLoading, setIsLoading] = useState(false);

  const handleToggle = async () => {
    setIsLoading(true);
    const result = isPaused ? await onUnpause() : await onPause();
    setIsLoading(false);

    if (result.status !== 'error') {
      toast.success(isPaused ? 'Contract unpaused!' : 'Contract paused!');
      await onRefresh();
    } else {
      toast.error(result.message || 'Operation failed');
    }
  };

  return (
    <Card className="border-l-4 border-l-purple-500">
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-4">
          <div className="p-3 bg-purple-500/20 rounded-xl">
            <Shield className="w-6 h-6 text-purple-400" />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-white">Contract Status</h2>
            <p className="text-sm text-slate-400">
              Current state: <span className={isPaused ? 'text-red-400' : 'text-green-400'}>
                {isPaused ? 'Paused' : 'Active'}
              </span>
            </p>
          </div>
        </div>
        <div className="flex items-center space-x-3">
          <Button
            variant="ghost"
            onClick={onRefresh}
            title="Refresh state"
          >
            <RefreshCw className="w-5 h-5" />
          </Button>
          {isPaused ? (
            <Button
              onClick={handleToggle}
              isLoading={isLoading}
              leftIcon={<PlayCircle className="w-5 h-5" />}
            >
              Unpause
            </Button>
          ) : (
            <Button
              variant="danger"
              onClick={handleToggle}
              isLoading={isLoading}
              leftIcon={<PauseCircle className="w-5 h-5" />}
            >
              Pause
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
}

// ============================================
// Owner Transfer Component
// ============================================

interface OwnerTransferProps {
  onTransfer: (newOwner: string) => Promise<TransactionResult>;
  onRefresh: () => Promise<void>;
}

function OwnerTransfer({ onTransfer, onRefresh }: OwnerTransferProps) {
  const [newOwner, setNewOwner] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showConfirmModal, setShowConfirmModal] = useState(false);

  const handleTransfer = async () => {
    if (!newOwner) {
      toast.error('Please enter new owner address');
      return;
    }
    setShowConfirmModal(true);
  };

  const confirmTransfer = async () => {
    setIsLoading(true);
    const result = await onTransfer(newOwner);
    setIsLoading(false);
    setShowConfirmModal(false);

    if (result.status !== 'error') {
      toast.success('Ownership transferred!');
      setNewOwner('');
      await onRefresh();
    } else {
      toast.error(result.message || 'Transfer failed');
    }
  };

  return (
    <>
      <Card>
        <div className="flex items-center space-x-3 mb-6">
          <div className="p-2 bg-orange-500/20 rounded-lg">
            <Key className="w-5 h-5 text-orange-400" />
          </div>
          <h2 className="text-xl font-bold text-white">Transfer Ownership</h2>
        </div>

        <div className="space-y-4">
          <Input
            label="New Owner Address"
            value={newOwner}
            onChange={(e) => setNewOwner(e.target.value)}
            placeholder="NEO address"
          />

          <div className="flex items-start space-x-2 p-3 bg-yellow-500/10 rounded-lg">
            <AlertTriangle className="w-4 h-4 text-yellow-400 flex-shrink-0 mt-0.5" />
            <p className="text-xs text-yellow-400">
              Ownership transfer is immediate. Double-check the address before confirming.
            </p>
          </div>

          <Button
            variant="secondary"
            onClick={handleTransfer}
            isLoading={isLoading}
            fullWidth
          >
            Transfer Ownership
          </Button>
        </div>
      </Card>

      <ConfirmModal
        isOpen={showConfirmModal}
        onClose={() => setShowConfirmModal(false)}
        onConfirm={confirmTransfer}
        title="Confirm Ownership Transfer"
        message={`Transfer ownership to ${shortenHash(newOwner)}? This takes effect immediately.`}
        confirmText="Transfer Ownership"
        isLoading={isLoading}
        variant="warning"
      />
    </>
  );
}

// ============================================
// Main Admin Component
// ============================================

export function Admin({
  connected,
  address,
  isOwner,
  isPaused,
  agents,
  owner,
  registerAgent,
  updateAgentTarget,
  updateAgentName,
  setAgentVoting,
  voteAgent,
  pause,
  unpause,
  transferOwner,
  fetchContractState,
}: AdminProps) {
  const [activeTab, setActiveTab] = useState<TabId>('agents');

  const handleSuccess = useCallback(() => {
    fetchContractState();
  }, [fetchContractState]);

  if (!connected) {
    return (
      <EmptyState
        icon={<Shield className="w-16 h-16" />}
        title="Admin Access Required"
        description="Please connect your wallet to access admin functions."
      />
    );
  }

  if (!isOwner) {
    return (
      <EmptyState
        icon={<AlertTriangle className="w-16 h-16 text-red-500/50" />}
        title="Unauthorized Access"
        description="You are not the contract owner. Admin functions are restricted to the owner only."
        action={
          <div className="text-sm text-slate-600 space-y-1">
            <p>Connected: {shortenHash(address || '')}</p>
            <p>Owner: {shortenHash(owner)}</p>
          </div>
        }
      />
    );
  }

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">Admin Panel</h1>
          <p className="text-slate-400">
            Manage TrustAnchor contract settings, agents, and voting configuration.
          </p>
        </div>
        <Badge variant="success" icon={<CheckCircle className="w-3 h-3" />}>
          Owner Access
        </Badge>
      </div>

      {/* Contract Status */}
      <ContractControls
        isPaused={isPaused}
        onPause={pause}
        onUnpause={unpause}
        onRefresh={fetchContractState}
      />

      {/* Tabs */}
      <div className="flex space-x-1 bg-slate-800/50 p-1 rounded-xl">
        {TABS.map((tab) => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`
                flex-1 flex items-center justify-center space-x-2 px-4 py-3 rounded-lg font-medium transition-all
                ${activeTab === tab.id
                  ? 'bg-slate-700 text-white'
                  : 'text-slate-400 hover:text-slate-200'
                }
              `}
            >
              <Icon className="w-4 h-4" />
              <span className="hidden sm:inline">{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      <div className="space-y-6">
        {activeTab === 'agents' && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card header={{ title: 'Register New Agent', icon: <UserPlus className="w-5 h-5 text-green-400" /> }}>
              <RegisterAgentForm
                onRegister={registerAgent}
                onSuccess={handleSuccess}
              />
            </Card>

            <Card header={{ title: 'Update Agent', icon: <Settings className="w-5 h-5 text-blue-400" /> }}>
              <UpdateAgentForm
                agents={agents}
                onUpdateTarget={updateAgentTarget}
                onUpdateName={updateAgentName}
                onSuccess={handleSuccess}
              />
            </Card>
          </div>
        )}

        {activeTab === 'voting' && (
          <Card header={{ title: 'Voting Configuration', icon: <Vote className="w-5 h-5 text-yellow-400" /> }}>
            <VotingConfigForm
              agents={agents}
              onSetVoting={setAgentVoting}
              onVote={voteAgent}
              onSuccess={handleSuccess}
            />
          </Card>
        )}

        {activeTab === 'contract' && (
          <OwnerTransfer
            onTransfer={transferOwner}
            onRefresh={fetchContractState}
          />
        )}
      </div>
    </div>
  );
}

export default Admin;
