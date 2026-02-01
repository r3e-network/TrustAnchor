import { forwardRef, type HTMLAttributes, type ReactNode } from 'react';

// ============================================
// Card Component
// ============================================

type CardVariant = 'default' | 'hover' | 'bordered' | 'glass';
type CardPadding = 'none' | 'sm' | 'md' | 'lg';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  variant?: CardVariant;
  padding?: CardPadding;
  header?: {
    title: string;
    icon?: ReactNode;
    action?: ReactNode;
  };
}

const variantClasses: Record<CardVariant, string> = {
  default: 'bg-slate-800/80 border border-slate-700/50',
  hover: 'bg-slate-800/80 border border-slate-700/50 hover:shadow-xl hover:shadow-green-500/5 hover:-translate-y-1 transition-all duration-300',
  bordered: 'bg-transparent border-2 border-slate-700',
  glass: 'bg-slate-800/50 backdrop-blur-lg border border-slate-700/50',
};

const paddingClasses: Record<CardPadding, string> = {
  none: '',
  sm: 'p-4',
  md: 'p-6',
  lg: 'p-8',
};

export const Card = forwardRef<HTMLDivElement, CardProps>(
  ({ 
    children, 
    variant = 'default', 
    padding = 'md',
    header,
    className = '',
    ...props 
  }, ref) => {
    const classes = [
      'rounded-xl',
      variantClasses[variant],
      paddingClasses[padding],
      className,
    ].join(' ');

    return (
      <div ref={ref} className={classes} {...props}>
        {header && (
          <CardHeader 
            title={header.title} 
            icon={header.icon} 
            action={header.action}
            className="mb-6"
          />
        )}
        {children}
      </div>
    );
  }
);

Card.displayName = 'Card';

// ============================================
// Card Header Component
// ============================================

interface CardHeaderProps {
  title?: string;
  icon?: ReactNode;
  action?: ReactNode;
  className?: string;
}

export function CardHeader({ title, icon, action, className = '' }: CardHeaderProps) {
  return (
    <div className={`flex items-center justify-between ${className}`}>
      <div className="flex items-center space-x-3">
        {icon && (
          <div className="p-2 bg-slate-700/50 rounded-lg">
            {icon}
          </div>
        )}
        {title && <h2 className="text-xl font-bold text-white">{title}</h2>}
      </div>
      {action}
    </div>
  );
}

// ============================================
// Card Content Component
// ============================================

interface CardContentProps {
  children: ReactNode;
  className?: string;
}

export function CardContent({ children, className = '' }: CardContentProps) {
  return (
    <div className={className}>
      {children}
    </div>
  );
}

export default Card;
