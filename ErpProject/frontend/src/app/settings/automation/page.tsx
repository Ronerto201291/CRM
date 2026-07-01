export default function AutomationSettingsPage() {
    return (
        <div className="p-8 space-y-8">
            <div className="flex justify-between items-center">
                <h1 className="text-3xl font-bold text-gray-800">Motor de Automatización</h1>
                <button className="bg-blue-600 text-white px-4 py-2 rounded shadow hover:bg-blue-700 transition">Crear Regla</button>
            </div>

            <div className="bg-white rounded-xl shadow border border-gray-100 overflow-hidden p-6">
                <p className="text-gray-500 mb-6">Configura reglas basadas en condiciones para disparar acciones en tu ERP.</p>

                <table className="min-w-full divide-y divide-gray-200 border rounded-lg overflow-hidden">
                    <thead className="bg-gray-50">
                        <tr>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Nombre de Regla</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Evento Trigger</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Condiciones</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                        </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                        <tr>
                            <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">Aviso Factura Grande</td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">OnInvoiceCreated</td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">Total &gt; 10000</td>
                            <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">Enviar Email a Admin</td>
                            <td className="px-6 py-4 whitespace-nowrap">
                                <span className="px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-green-100 text-green-800">Activo</span>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>
    );
}
