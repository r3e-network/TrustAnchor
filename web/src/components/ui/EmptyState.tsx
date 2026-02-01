import type { ReactNode } from 'react';
import { FileX } from 'lucide-react';

// ============================================
// Empty State Component
// ============================================

interface EmptyStateProps {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ 
  icon = <FileX className="w-16 h-16" />, 
  title, 
  description,
  action,
  className = '' 
}: EmptyStateProps) {
  return (
    <div className={`text-center py-12 ${className}`}>
      <div className="text-slate-600 mx-auto mb-4">
        {icon}
      </div>
      <h3 className="text-xl font-semibold text-white mb-2">{title}</h3>
      {description && <p className="text-slate-500 max-w-md mx-auto mb-6">{description}</p>}
      {action}
    </div>
  );
}

export default EmptyState;
