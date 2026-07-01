export default function Logo({ size = 48, className = "" }: { size?: number, className?: string }) {
  // Use the AI-generated png logo, applying mix-blend-mode if needed to fake transparency
  return (
    <img 
      src="/logo.png" 
      alt="Orbital ERP Logo"
      className={className} 
      style={{ 
        width: size, 
        height: size, 
        objectFit: 'contain',
        display: 'inline-block',
        flexShrink: 0,
        borderRadius: '12px'
      }} 
    />
  );
}
