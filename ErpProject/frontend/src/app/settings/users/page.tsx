import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import UsersClient from './UsersClient';

interface User {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    roleId: string | null;
    roleName: string | null;
    isActive: boolean;
    twoFactorEnabled: boolean;
    createdAt: string;
}

interface Role {
    id: string;
    name: string;
}

export default async function UsersPage() {
    const [initialUsers, initialRoles] = await Promise.all([
        serverFetchList<User>('users'),
        serverFetch<Role[]>('users/roles'),
    ]);
    return (
        <UsersClient
            initialUsers={initialUsers}
            initialRoles={Array.isArray(initialRoles) ? initialRoles : []}
        />
    );
}
