import { X, AlertTriangle } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from './Button';

// ============================================
// Modal Component
// ============================================

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  icon?: ReactNode;
  iconVariant?: 'default' | 'danger' | 'warning';
}

const iconVariantClasses = {
  default: 'bg-blue-500/20',
  danger: 'bg-red-500/20',
  warning: 'bg-yellow-500/20',
};

const iconColorClasses = {
  default: 'text-blue-400',
  danger: 'text-red-400',
  warning: 'text-yellow-400',
};

export function Modal({ 
  isOpen, 
  onClose, 
  title, 
  children, 
  icon,
  iconVariant = 'default' 
}: ModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-black/70 backdrop-blur-sm"
        onClick={onClose}
      />
      
      {/* Modal */}
      <div className="relative bg-slate-800 rounded-2xl border border-slate-700 max-w-md w-full max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-slate-700">
          <div className="flex items-center space-x-3">
            {icon && (
              <div className={`p-2 rounded-lg ${iconVariantClasses[iconVariant]}`}>
                <span className={iconColorClasses[iconVariant]}>{icon}</span>
              </div>
            )}
            <h3 className="text-xl font-bold text-white">{title}</h3>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white hover:bg-slate-700 rounded-lg transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        
        {/* Content */}
        <div className="p-6">
          {children}
        </div>
      </div>
    </div>
  );
}

// Confirmation Modal
interface ConfirmModalProps extends Omit<ModalProps, 'children'> {
  message: string;
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void;
  isLoading?: boolean;
  variant?: 'danger' | 'warning' | 'default';
}

export function ConfirmModal({
  isOpen,
  onClose,
  title,
  message,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  onConfirm,
  isLoading = false,
  variant = 'default',
}: ConfirmModalProps) {
  const variantConfig = {
    danger: { icon: <AlertTriangle className="w-6 h-6" />, iconVariant: 'danger' as const, buttonVariant: 'danger' as const },
    warning: { icon: <AlertTriangle className="w-6 h-6" />, iconVariant: 'warning' as const, buttonVariant: 'primary' as const },
    default: { icon: undefined, iconVariant: 'default' as const, buttonVariant: 'primary' as const },
  };

  const config = variantConfig[variant];

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      icon={config.icon}
      iconVariant={config.iconVariant}
    >
      <div className="space-y-6">
        <p className="text-slate-300">{message}</p>
        
        <div className="flex space-x-3">
          <Button
            variant="secondary"
            onClick={onClose}
            fullWidth
            disabled={isLoading}
          >
            {cancelText}
          </Button>
          <Button
            variant={config.buttonVariant}
            onClick={onConfirm}
            fullWidth
            isLoading={isLoading}
          >
            {confirmText}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

export default Modal;
