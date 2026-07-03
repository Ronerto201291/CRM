import { serverFetchList } from '@/lib/serverFetch';
import ContactsClient, { type Contact } from './ContactsClient';

export default async function ContactsPage() {
    const initialContacts = await serverFetchList<Contact>('contacts?pageSize=500');
    return <ContactsClient initialContacts={initialContacts} />;
}
