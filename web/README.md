# TrustAnchor Web Interface

A modern, comprehensive web interface for the TrustAnchor NEO voting delegation system.

## Features

### Wallet Integration
- NeoLine wallet support for Neo N3
- Automatic provider detection
- Network switching (Testnet/Mainnet)
- Secure transaction signing

### Dashboard
- Real-time contract statistics
- Total staked NEO display
- Active agents overview
- Personal staking position
- Pool share calculation

### Staking
- Deposit NEO into TrustAnchor
- Withdraw staked NEO
- Claim GAS rewards
- Emergency withdrawal (when paused)
- Form validation and error handling

### Agents Management
- View all registered agents
- Search agents by name/address/target
- Owner-only voting controls
- Priority status display
- Workflow explanation

### Admin Panel (Owner Only)
- Register new agents
- Update agent targets and names
- Configure voting priorities
- Cast votes for agents
- Pause/unpause contract
- Transfer contract ownership

## Architecture

```
web/
├── src/
│   ├── abis/
│   │   └── TrustAnchor.ts       # Contract constants & utilities
│   ├── components/
│   │   ├── ui/                  # Reusable UI components
│   │   │   ├── Button.tsx
│   │   │   ├── Card.tsx
│   │   │   ├── Input.tsx
│   │   │   ├── Badge.tsx
│   │   │   ├── Modal.tsx
│   │   │   ├── Loading.tsx
│   │   │   ├── StatCard.tsx
│   │   │   ├── EmptyState.tsx
│   │   │   └── index.ts
│   │   ├── Layout.tsx           # Main layout with navigation
│   │   └── index.ts
│   ├── hooks/
│   │   ├── useWallet.ts         # Wallet connection hook
│   │   └── useTrustAnchor.ts    # Contract interaction hook
│   ├── pages/
│   │   ├── Dashboard.tsx        # Main dashboard page
│   │   ├── Staking.tsx          # Staking operations page
│   │   ├── Agents.tsx           # Agent management page
│   │   └── Admin.tsx            # Admin panel page
│   ├── types/
│   │   └── index.ts             # TypeScript type definitions
│   ├── App.tsx                  # Main application component
│   ├── main.tsx                 # Application entry point
│   └── index.css                # Global styles
├── public/
│   └── favicon.svg
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
└── tailwind.config.js
```

## Tech Stack

- **Framework**: React 18 with TypeScript
- **Routing**: React Router v6
- **Styling**: Tailwind CSS
- **Icons**: Lucide React
- **Blockchain**: neon-js / neon-core
- **Build Tool**: Vite
- **Notifications**: react-hot-toast

## Getting Started

### Prerequisites
- Node.js 18+
- npm or yarn
- NeoLine browser extension

### Installation

```bash
cd web
npm install
```

### Development

```bash
npm run dev
```

The application will be available at `http://localhost:3000`

### Build for Production

```bash
npm run build
```

Output will be in the `dist/` directory.

## Code Quality

### Type Safety
- Full TypeScript coverage
- Strict type checking enabled
- Comprehensive type definitions in `src/types/`

### Component Architecture
- Small, focused components
- Props interface for each component
- Forward refs where appropriate
- Consistent naming conventions

### Error Handling
- Error boundary at root level
- Form validation utilities
- User-friendly error messages
- Toast notifications for feedback

### Performance
- Lazy loading for routes
- useMemo for expensive calculations
- useCallback for event handlers
- Optimized re-renders

## Smart Contract Integration

### View Methods
- `owner()` - Get contract owner
- `agentCount()` - Get number of agents
- `agent(i)` - Get agent by index
- `totalStake()` - Get total staked NEO
- `stakeOf(address)` - Get user's stake
- `reward(address)` - Get user's claimable rewards
- `isPaused()` - Get contract pause state

### Write Methods (User)
- `deposit()` - Stake NEO (via transfer)
- `withdraw(amount)` - Unstake NEO
- `claimReward()` - Claim GAS rewards
- `emergencyWithdraw()` - Emergency withdrawal

### Write Methods (Owner)
- `registerAgent(hash, target, name)` - Register new agent
- `updateAgentTargetById(index, target)` - Update agent target
- `updateAgentNameById(index, name)` - Update agent name
- `setAgentVotingById(index, amount)` - Set voting priority
- `voteAgentById(index)` - Cast vote for agent
- `pause()` / `unpause()` - Contract control
- `transferOwner(newOwner)` - Transfer ownership immediately

## Security Features

- Wallet connection required for sensitive operations
- Owner-only access to admin functions
- Form validation before submission
- Confirmation modals for critical actions
- Address format validation
- Public key validation

## Styling

### Design System
- Dark theme with slate color palette
- Green accents for primary actions
- Consistent spacing and sizing
- Responsive layout for all screen sizes
- Hover and focus states

### CSS Classes
- Tailwind utility classes
- Custom animations (fade-in, pulse-glow)
- Glass morphism effects
- Gradient backgrounds
