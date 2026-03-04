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
  default: 'bg-neo-gray border border-neo-light',
  hover: 'bg-neo-gray border border-neo-light hover:shadow-xl hover:shadow-neo-green/5 hover:-translate-y-1 hover:border-neo-green/50 transition-all duration-300',
  bordered: 'bg-transparent border-2 border-neo-light',
  glass: 'bg-neo-gray/80 backdrop-blur-lg border border-neo-light/50',
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
          <div className="p-2 bg-neo-light/50 rounded-lg">
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
