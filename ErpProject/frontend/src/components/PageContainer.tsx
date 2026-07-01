'use client';

interface PageContainerProps {
  children: React.ReactNode;
  style?: React.CSSProperties;
}

export default function PageContainer({ children, style }: PageContainerProps) {
  return (
    <div style={{
      padding: '24px 32px',
      fontFamily: 'Inter, sans-serif',
      ...style,
    }}>
      {children}
    </div>
  );
}
