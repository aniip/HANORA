import { api } from './api'
import type { PagedResult } from './reporting'

export type StaffRole = 'Owner' | 'Cashier'

export type StaffUser = {
  id: string
  name: string
  email: string
  role: string
  isActive: boolean
  mustChangePassword: boolean
  lastLoginAt: string | null
  createdAt: string
}

export type CreateStaffUserInput = {
  name: string
  email: string
  password: string
  role: StaffRole
}

export type UpdateStaffUserInput = {
  name: string
  email: string
  role: StaffRole
}

export async function getStaffUsers(
  page = 1,
  pageSize = 50,
): Promise<PagedResult<StaffUser>> {
  const response = await api.get<PagedResult<StaffUser>>('/owner/users', {
    params: { page, pageSize },
  })
  return response.data
}

export async function createStaffUser(
  input: CreateStaffUserInput,
): Promise<StaffUser> {
  const response = await api.post<StaffUser>('/owner/users', input)
  return response.data
}

export async function updateStaffUser(
  id: string,
  input: UpdateStaffUserInput,
): Promise<StaffUser> {
  const response = await api.put<StaffUser>(`/owner/users/${id}`, input)
  return response.data
}

export async function updateStaffUserStatus(
  id: string,
  isActive: boolean,
): Promise<StaffUser> {
  const response = await api.patch<StaffUser>(`/owner/users/${id}/status`, {
    isActive,
  })
  return response.data
}
