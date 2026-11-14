import { api } from './api'

export type TableInfo = {
  id: string
  number: string
  name: string
  capacity: number
}

export type Table = TableInfo & {
  isActive: boolean
  qrUrl: string | null
  qrCreatedAt: string | null
}

export type TableInput = {
  number: string
  name: string
  capacity: number
  isActive: boolean
}

export async function getOwnerTables(): Promise<Table[]> {
  const response = await api.get<Table[]>('/owner/tables')
  return response.data
}

export async function createTable(input: TableInput): Promise<Table> {
  const response = await api.post<Table>('/owner/tables', input)
  return response.data
}

export async function updateTable(
  id: string,
  input: TableInput,
): Promise<Table> {
  const response = await api.put<Table>(`/owner/tables/${id}`, input)
  return response.data
}

export async function deleteTable(id: string): Promise<void> {
  await api.delete(`/owner/tables/${id}`)
}

export async function regenerateTableQr(id: string): Promise<Table> {
  const response = await api.post<Table>(`/owner/tables/${id}/qr/regenerate`)
  return response.data
}

export async function getTableQr(
  id: string,
  format: 'png' | 'svg' = 'png',
): Promise<Blob> {
  const response = await api.get<Blob>(`/owner/tables/${id}/qr`, {
    params: { format },
    responseType: 'blob',
  })

  return response.data
}
