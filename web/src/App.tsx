import { Routes, Route, Navigate } from "react-router-dom";
import { useEffect, Suspense, lazy } from "react";
import { useTrustAnchor } from "./hooks/useTrustAnchor";
import { Layout } from "./components";

// Lazy load pages for better performance
const Dashboard = lazy(() => import("./pages/Dashboard"));
const Staking = lazy(() => import("./pages/Staking"));
const Agents = lazy(() => import("./pages/Agents"));
const Admin = lazy(() => import("./pages/Admin"));

// ============================================
// Loading Fallback
// ============================================

function PageLoader() {
  return (
    <div className="flex items-center justify-center py-20">
      <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-green-500"></div>
    </div>
  );
}

// ============================================
// Main App Component
// ============================================

function App() {
  const trustAnchor = useTrustAnchor();
  const { contractHash, fetchContractState, connected, address, fetchUserStakeInfo } = trustAnchor;

  // Fetch contract state on mount and network change
  useEffect(() => {
    if (contractHash) {
      fetchContractState();
    }
  }, [contractHash, fetchContractState]);

  // Fetch user stake info when connected
  useEffect(() => {
    if (connected && address) {
      fetchUserStakeInfo(address);
    }
  }, [connected, address, fetchUserStakeInfo]);

  const layoutProps = {
    ...trustAnchor,
  };

  return (
    <Layout {...layoutProps}>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route
            path="/"
            element={
              <Dashboard
                connected={trustAnchor.connected}
                address={trustAnchor.address}
                contractHash={trustAnchor.contractHash}
                owner={trustAnchor.owner}
                isPaused={trustAnchor.isPaused}
                agents={trustAnchor.agents}
                stakeInfo={trustAnchor.stakeInfo}
                loading={trustAnchor.loading}
                isOwner={trustAnchor.isOwner}
                network={trustAnchor.network}
                fetchContractState={trustAnchor.fetchContractState}
                fetchUserStakeInfo={trustAnchor.fetchUserStakeInfo}
              />
            }
          />
          <Route
            path="/staking"
            element={
              <Staking
                connected={trustAnchor.connected}
                isPaused={trustAnchor.isPaused}
                stakeInfo={trustAnchor.stakeInfo}
                deposit={trustAnchor.deposit}
                withdraw={trustAnchor.withdraw}
                claimReward={trustAnchor.claimReward}
                emergencyWithdraw={trustAnchor.emergencyWithdraw}
              />
            }
          />
          <Route
            path="/agents"
            element={
              <Agents
                agents={trustAnchor.agents}
                isOwner={trustAnchor.isOwner}
                loading={trustAnchor.loading}
                voteAgent={trustAnchor.voteAgent}
              />
            }
          />
          <Route
            path="/admin"
            element={
              <Admin
                connected={trustAnchor.connected}
                address={trustAnchor.address}
                isOwner={trustAnchor.isOwner}
                isPaused={trustAnchor.isPaused}
                agents={trustAnchor.agents}
                owner={trustAnchor.owner}
                registerAgent={trustAnchor.registerAgent}
                updateAgentTarget={trustAnchor.updateAgentTarget}
                updateAgentName={trustAnchor.updateAgentName}
                setAgentVoting={trustAnchor.setAgentVoting}
                voteAgent={trustAnchor.voteAgent}
                pause={trustAnchor.pause}
                unpause={trustAnchor.unpause}
                proposeOwner={trustAnchor.proposeOwner}
                acceptOwner={trustAnchor.acceptOwner}
                cancelOwnerProposal={trustAnchor.cancelOwnerProposal}
                fetchContractState={trustAnchor.fetchContractState}
              />
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </Layout>
  );
}

export default App;
