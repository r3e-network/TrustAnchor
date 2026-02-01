import type { ReactNode } from 'react';
import { Card } from './Card';

// ============================================
// Stat Card Component
// ============================================

interface StatCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  icon: ReactNode;
  iconColor?: 'blue' | 'green' | 'purple' | 'yellow' | 'red';
  isLoading?: boolean;
}

const iconColorClasses = {
  blue: 'bg-blue-500/20 text-blue-400',
  green: 'bg-green-500/20 text-green-400',
  purple: 'bg-purple-500/20 text-purple-400',
  yellow: 'bg-yellow-500/20 text-yellow-400',
  red: 'bg-red-500/20 text-red-400',
};

export function StatCard({ 
  title, 
  value, 
  subtitle, 
  icon, 
  iconColor = 'blue',
  isLoading = false 
}: StatCardProps) {
  return (
    <Card variant="hover">
      <div className="flex items-center justify-between mb-4">
        <div className={`p-3 rounded-lg ${iconColorClasses[iconColor]}`}>
          {icon}
        </div>
        <span className="text-xs text-slate-500 font-medium uppercase tracking-wider">
          {title}
        </span>
      </div>
      
      {isLoading ? (
        <div className="animate-pulse">
          <div className="h-8 bg-slate-700/50 rounded w-24" />
          {subtitle && <div className="h-4 bg-slate-700/50 rounded w-16 mt-2" />}
        </div>
      ) : (
        <>
          <div className="text-3xl font-bold text-white">{value}</div>
          {subtitle && <div className="text-sm text-slate-400 mt-1">{subtitle}</div>}
        </>
      )}
    </Card>
  );
}

export default StatCard;
