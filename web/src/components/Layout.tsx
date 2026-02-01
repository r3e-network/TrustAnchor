import { Link, useLocation } from 'react-router-dom';
import { 
  Wallet, 
  LayoutDashboard, 
  Coins, 
  Users, 
  Shield, 
  ChevronDown, 
  ExternalLink,
  Menu,
  X,
  LogOut,
  Network
} from 'lucide-react';
import { useState, useCallback, useEffect } from 'react';
import { Badge } from './ui';
import type { NetworkType, WalletState } from '../types';

// ============================================
// Navigation Configuration
// ============================================

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: typeof LayoutDashboard;
  readonly requiresOwner?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { path: '/', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/staking', label: 'Staking', icon: Coins },
  { path: '/agents', label: 'Agents', icon: Users },
  { path: '/admin', label: 'Admin', icon: Shield, requiresOwner: true },
];

// ============================================
// Layout Component Props
// ============================================

interface LayoutProps extends WalletState {
  readonly children: React.ReactNode;
  readonly network: NetworkType;
  readonly setNetwork: (network: NetworkType) => void;
  readonly connect: () => Promise<boolean>;
  readonly disconnect: () => void;
  readonly isOwner: boolean;
}

// ============================================
// Utility Functions
// ============================================

const formatAddress = (addr: string): string => {
  if (addr.length < 12) return addr;
  return `${addr.slice(0, 6)}...${addr.slice(-4)}`;
};

// ============================================
// Navigation Link Component
// ============================================

interface NavLinkProps {
  item: NavItem;
  isActive: boolean;
  onClick?: () => void;
}

function NavLink({ item, isActive, onClick }: NavLinkProps) {
  const Icon = item.icon;
  
  return (
    <Link
      to={item.path}
      onClick={onClick}
      className={`
        flex items-center space-x-2 px-4 py-2 rounded-lg font-medium transition-all duration-200
        ${isActive 
          ? 'bg-green-500/20 text-green-400' 
          : 'text-slate-400 hover:text-slate-100 hover:bg-slate-800'
        }
      `}
    >
      <Icon className="w-4 h-4" />
      <span>{item.label}</span>
    </Link>
  );
}

// ============================================
// Main Layout Component
// ============================================

export function Layout({ 
  children, 
  network, 
  setNetwork,
  connected,
  connecting,
  address,
  connect,
  disconnect,
  isOwner
}: LayoutProps) {
  const location = useLocation();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [networkDropdownOpen, setNetworkDropdownOpen] = useState(false);

  // Close mobile menu on route change
  useEffect(() => {
    setMobileMenuOpen(false);
  }, [location.pathname]);

  // Close dropdowns on outside click
  useEffect(() => {
    const handleClickOutside = () => {
      setNetworkDropdownOpen(false);
    };
    
    if (networkDropdownOpen) {
      document.addEventListener('click', handleClickOutside);
      return () => document.removeEventListener('click', handleClickOutside);
    }
  }, [networkDropdownOpen]);

  const handleConnect = useCallback(async () => {
    try {
      await connect();
    } catch (error) {
      console.error('Connection error:', error);
    }
  }, [connect]);

  // Filter nav items based on ownership
  const visibleNavItems = NAV_ITEMS.filter(item => 
    !item.requiresOwner || (item.requiresOwner && isOwner)
  );

  const currentNetworkLabel = network === 'testnet' ? 'Testnet' : 'Mainnet';

  return (
    <div className="min-h-screen bg-slate-900 flex flex-col">
      {/* Header */}
      <header className="fixed top-0 left-0 right-0 z-50 bg-slate-900/80 backdrop-blur-lg border-b border-slate-700/50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            {/* Logo */}
            <Link to="/" className="flex items-center space-x-3">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-green-500 to-emerald-600 flex items-center justify-center shadow-lg shadow-green-500/20">
                <Shield className="w-6 h-6 text-white" />
              </div>
              <span className="text-xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-green-400 to-emerald-500 hidden sm:block">
                TrustAnchor
              </span>
            </Link>

            {/* Desktop Navigation */}
            <nav className="hidden md:flex items-center space-x-1">
              {visibleNavItems.map((item) => (
                <NavLink
                  key={item.path}
                  item={item}
                  isActive={location.pathname === item.path}
                />
              ))}
            </nav>

            {/* Right side actions */}
            <div className="flex items-center space-x-3">
              {/* Network Selector */}
              <div className="relative" onClick={(e) => e.stopPropagation()}>
                <button
                  onClick={() => setNetworkDropdownOpen(!networkDropdownOpen)}
                  className="flex items-center space-x-2 px-3 py-2 bg-slate-800 rounded-lg text-sm font-medium text-slate-300 hover:bg-slate-700 transition-colors"
                >
                  <Network className="w-4 h-4" />
                  <span className="hidden sm:inline">{currentNetworkLabel}</span>
                  <ChevronDown className={`w-4 h-4 transition-transform ${networkDropdownOpen ? 'rotate-180' : ''}`} />
                </button>
                
                {networkDropdownOpen && (
                  <div className="absolute right-0 mt-2 w-40 bg-slate-800 rounded-xl border border-slate-700 shadow-xl z-20 py-1">
                    {(['testnet', 'mainnet'] as NetworkType[]).map((net) => (
                      <button
                        key={net}
                        onClick={() => {
                          setNetwork(net);
                          setNetworkDropdownOpen(false);
                        }}
                        className={`
                          w-full px-4 py-2 text-left text-sm hover:bg-slate-700 transition-colors
                          ${network === net ? 'text-green-400' : 'text-slate-300'}
                        `}
                      >
                        {net === 'testnet' ? 'Testnet' : 'Mainnet'}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* Wallet Connection */}
              {connected ? (
                <div className="flex items-center space-x-2">
                  <div className="hidden sm:flex items-center space-x-2 px-4 py-2 bg-slate-800 rounded-lg">
                    <Wallet className="w-4 h-4 text-green-400" />
                    <span className="text-sm font-medium text-slate-300">
                      {formatAddress(address!)}
                    </span>
                    {isOwner && (
                      <Badge variant="purple" size="sm">Owner</Badge>
                    )}
                  </div>
                  <button
                    onClick={disconnect}
                    className="p-2 bg-slate-800 rounded-lg text-slate-400 hover:text-red-400 hover:bg-slate-700 transition-colors"
                    title="Disconnect"
                  >
                    <LogOut className="w-5 h-5" />
                  </button>
                </div>
              ) : (
                <button
                  onClick={handleConnect}
                  disabled={connecting}
                  className="px-6 py-3 bg-gradient-to-r from-green-500 to-emerald-600 text-white font-semibold rounded-lg hover:from-green-600 hover:to-emerald-700 transition-all duration-200 shadow-lg shadow-green-500/20 disabled:opacity-50 disabled:cursor-not-allowed flex items-center space-x-2"
                >
                  <Wallet className="w-4 h-4" />
                  <span>{connecting ? 'Connecting...' : 'Connect'}</span>
                </button>
              )}

              {/* Mobile menu button */}
              <button
                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                className="md:hidden p-2 bg-slate-800 rounded-lg text-slate-400 hover:text-slate-100"
              >
                {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          </div>
        </div>

        {/* Mobile Navigation */}
        {mobileMenuOpen && (
          <div className="md:hidden border-t border-slate-700/50">
            <div className="px-4 py-3 space-y-1">
              {visibleNavItems.map((item) => (
                <NavLink
                  key={item.path}
                  item={item}
                  isActive={location.pathname === item.path}
                  onClick={() => setMobileMenuOpen(false)}
                />
              ))}
            </div>
          </div>
        )}
      </header>

      {/* Main Content */}
      <main className="flex-1 pt-16">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          {children}
        </div>
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div className="flex flex-col md:flex-row items-center justify-between space-y-4 md:space-y-0">
            <div className="flex items-center space-x-2 text-slate-500 text-sm">
              <Shield className="w-4 h-4" />
              <span>TrustAnchor - Non-Profit NEO Voting Delegation</span>
            </div>
            <div className="flex items-center space-x-6 text-sm">
              <a 
                href="https://github.com/r3e-network/TrustAnchor" 
                target="_blank" 
                rel="noopener noreferrer"
                className="flex items-center space-x-1 text-slate-500 hover:text-slate-300 transition-colors"
              >
                <span>GitHub</span>
                <ExternalLink className="w-3 h-3" />
              </a>
              <a 
                href="https://neo.org" 
                target="_blank" 
                rel="noopener noreferrer"
                className="flex items-center space-x-1 text-slate-500 hover:text-slate-300 transition-colors"
              >
                <span>NEO</span>
                <ExternalLink className="w-3 h-3" />
              </a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default Layout;
