import { type ReactNode } from 'react';

interface FormLabelProps {
    htmlFor: string;
    children: ReactNode;
    required?: boolean;
}

/** Label accesible con htmlFor (ADR-0018 #48). */
export default function FormLabel({ htmlFor, children, required }: FormLabelProps) {
    return (
        <label
            htmlFor={htmlFor}
            className="erp-label"
            style={{ display: 'block', marginBottom: '6px' }}
        >
            {children}{required ? ' *' : ''}
        </label>
    );
}
