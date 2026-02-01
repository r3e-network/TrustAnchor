import { type ReactNode } from 'react';

// ============================================
// Badge Component
// ============================================

type BadgeVariant = 'success' | 'warning' | 'error' | 'info' | 'neutral' | 'purple';
type BadgeSize = 'sm' | 'md';

interface BadgeProps {
  children: ReactNode;
  variant?: BadgeVariant;
  size?: BadgeSize;
  icon?: ReactNode;
  className?: string;
}

const variantClasses: Record<BadgeVariant, string> = {
  success: 'bg-green-500/20 text-green-400 border-green-500/30',
  warning: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
  error: 'bg-red-500/20 text-red-400 border-red-500/30',
  info: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
  neutral: 'bg-slate-500/20 text-slate-400 border-slate-500/30',
  purple: 'bg-purple-500/20 text-purple-400 border-purple-500/30',
};

const sizeClasses: Record<BadgeSize, string> = {
  sm: 'px-2 py-0.5 text-xs',
  md: 'px-2.5 py-1 text-sm',
};

export function Badge({ 
  children, 
  variant = 'neutral', 
  size = 'md',
  icon,
  className = '' 
}: BadgeProps) {
  const classes = [
    'inline-flex items-center space-x-1.5 rounded-full font-medium border',
    variantClasses[variant],
    sizeClasses[size],
    className,
  ].join(' ');

  return (
    <span className={classes}>
      {icon && <span>{icon}</span>}
      <span>{children}</span>
    </span>
  );
}

export default Badge;
