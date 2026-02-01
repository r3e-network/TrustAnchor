import { Loader2 } from 'lucide-react';
import type { ReactNode } from 'react';

// ============================================
// Loading Components
// ============================================

interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeClasses = {
  sm: 'w-4 h-4',
  md: 'w-8 h-8',
  lg: 'w-12 h-12',
};

export function LoadingSpinner({ size = 'md', className = '' }: LoadingSpinnerProps) {
  return (
    <Loader2 className={`animate-spin text-green-500 ${sizeClasses[size]} ${className}`} />
  );
}

// Loading Overlay
interface LoadingOverlayProps {
  children?: ReactNode;
  text?: string;
}

export function LoadingOverlay({ children, text = 'Loading...' }: LoadingOverlayProps) {
  return (
    <div className="flex flex-col items-center justify-center py-12">
      <LoadingSpinner size="lg" />
      {children || <p className="mt-4 text-slate-400">{text}</p>}
    </div>
  );
}

// Skeleton Loading
interface SkeletonProps {
  className?: string;
}

export function Skeleton({ className = '' }: SkeletonProps) {
  return (
    <div className={`animate-pulse bg-slate-700/50 rounded ${className}`} />
  );
}

// Skeleton Card
export function SkeletonCard() {
  return (
    <div className="bg-slate-800/80 rounded-xl border border-slate-700/50 p-6 space-y-4">
      <div className="flex items-center space-x-3">
        <Skeleton className="w-12 h-12 rounded-lg" />
        <div className="space-y-2">
          <Skeleton className="w-32 h-4" />
          <Skeleton className="w-20 h-3" />
        </div>
      </div>
      <Skeleton className="w-full h-8" />
    </div>
  );
}

export default LoadingSpinner;
