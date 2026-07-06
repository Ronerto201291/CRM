import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';

export default async function HomePage() {
  const cookieStore = await cookies();
  if (cookieStore.get('erp_token')) {
    redirect('/dashboard');
  }

  redirect('/admin');
}
