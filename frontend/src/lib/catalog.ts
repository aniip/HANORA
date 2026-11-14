import { api } from './api'

export type Category = {
  id: string
  name: string
  slug: string
  sortOrder: number
  isActive: boolean
  productCount: number
}

export type Product = {
  id: string
  name: string
  slug: string
  description: string | null
  price: number
  imageUrl: string | null
  isAvailable: boolean
  isArchived: boolean
  categoryId: string
  categoryName: string
  categorySlug: string
  createdAt: string
  updatedAt: string
}

export type PagedProducts = {
  items: Product[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type PublicSite = {
  name: string
  tagline: string
  description: string
  address: string
  phone: string
  instagram: string
  openingHours: string
  mapsUrl: string
  logoUrl: string
}

export type CategoryInput = {
  name: string
  slug?: string
  sortOrder: number
  isActive: boolean
}

export type ProductInput = {
  name: string
  categoryId: string
  description: string
  price: number
  isAvailable: boolean
}

export type SiteSettingsInput = Omit<PublicSite, 'logoUrl'>

export async function getPublicSite(): Promise<PublicSite> {
  const response = await api.get<PublicSite>('/public/site')
  return response.data
}

export async function getPublicCategories(): Promise<Category[]> {
  const response = await api.get<Category[]>('/public/categories')
  return response.data
}

export async function getPublicProducts(params: {
  category?: string
  search?: string
  page?: number
  pageSize?: number
} = {}): Promise<PagedProducts> {
  const response = await api.get<PagedProducts>('/public/products', { params })
  return response.data
}

export async function getPublicProduct(slug: string): Promise<Product> {
  const response = await api.get<Product>(`/public/products/${slug}`)
  return response.data
}

export async function getOwnerCategories(): Promise<Category[]> {
  const response = await api.get<Category[]>('/owner/categories')
  return response.data
}

export async function createCategory(input: CategoryInput): Promise<Category> {
  const response = await api.post<Category>('/owner/categories', input)
  return response.data
}

export async function updateCategory(
  id: string,
  input: CategoryInput,
): Promise<Category> {
  const response = await api.put<Category>(`/owner/categories/${id}`, input)
  return response.data
}

export async function deleteCategory(id: string): Promise<void> {
  await api.delete(`/owner/categories/${id}`)
}

export async function getOwnerProducts(params: {
  categoryId?: string
  search?: string
  status?: string
  page?: number
  pageSize?: number
} = {}): Promise<PagedProducts> {
  const response = await api.get<PagedProducts>('/owner/products', { params })
  return response.data
}

export async function createProduct(input: ProductInput): Promise<Product> {
  const response = await api.post<Product>('/owner/products', input)
  return response.data
}

export async function updateProduct(
  id: string,
  input: ProductInput,
): Promise<Product> {
  const response = await api.put<Product>(`/owner/products/${id}`, input)
  return response.data
}

export async function setProductAvailability(
  id: string,
  isAvailable: boolean,
): Promise<Product> {
  const response = await api.patch<Product>(`/owner/products/${id}/availability`, {
    isAvailable,
  })
  return response.data
}

export async function archiveProduct(id: string): Promise<void> {
  await api.delete(`/owner/products/${id}`)
}

export async function uploadProductImage(
  id: string,
  file: File,
): Promise<Product> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await api.post<Product>(
    `/owner/products/${id}/image`,
    formData,
  )

  return response.data
}

export async function getOwnerSettings(): Promise<PublicSite> {
  const response = await api.get<PublicSite>('/owner/settings')
  return response.data
}

export async function updateOwnerSettings(
  input: SiteSettingsInput,
): Promise<PublicSite> {
  const response = await api.put<PublicSite>('/owner/settings', input)
  return response.data
}
