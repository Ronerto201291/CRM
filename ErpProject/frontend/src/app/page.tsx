import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import LoginForm from './LoginForm';

export default async function LoginPage() {
  const cookieStore = await cookies();
  if (cookieStore.get('erp_token')) {
    redirect('/dashboard');
  }

  return <LoginForm />;
}
