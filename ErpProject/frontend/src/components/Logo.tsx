import Image from 'next/image';

export default function Logo({ size = 48, className = "" }: { size?: number, className?: string }) {
  return (
    <Image
      src="/logo.png"
      alt="Orbital ERP Logo"
      width={size}
      height={size}
      className={className}
      style={{
        objectFit: 'contain',
        display: 'inline-block',
        flexShrink: 0,
        borderRadius: '12px',
      }}
    />
  );
}
