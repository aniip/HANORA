import { api } from './api'

export type DashboardSummary = {
  from: string
  to: string
  totalRevenue: number
  totalTransactions: number
  activeOrders: number
  totalExpenses: number
  netProfit: number
  averageOrderValue: number
  qrisRevenue: number
  cashRevenue: number
}

export type SalesTrendPoint = {
  period: string
  revenue: number
  transactions: number
}

export type TopProduct = {
  productId: string | null
  productName: string
  quantitySold: number
  revenue: number
}

export type PaymentMethodBreakdown = {
  method: string
  revenue: number
  transactions: number
}

export type Expense = {
  id: string
  category: string
  description: string
  amount: number
  occurredAt: string
  note: string | null
  createdByName: string | null
  createdAt: string
  updatedAt: string
}

export type ExpenseInput = {
  category: string
  description: string
  amount: number
  occurredAt: string
  note?: string
}

export type Income = {
  id: string
  source: 'order' | 'manual'
  description: string
  amount: number
  occurredAt: string
  orderNumber: string | null
  createdByName: string | null
  createdAt: string
}

export type ManualIncomeInput = {
  description: string
  amount: number
  occurredAt: string
}

export type ReportTransaction = {
  orderNumber: string
  customerName: string
  tableName: string
  total: number
  method: string
  status: string
  paidAt: string | null
}

export type ReportData = {
  summary: DashboardSummary
  transactions: ReportTransaction[]
  expenses: Expense[]
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type ReportFormat = 'pdf' | 'xlsx'
export type ReportType = 'summary' | 'transactions' | 'expenses' | 'full'

type DateRange = {
  from: string
  to: string
}

export async function getDashboardSummary(
  range: DateRange,
): Promise<DashboardSummary> {
  const response = await api.get<DashboardSummary>('/owner/dashboard/summary', {
    params: range,
  })
  return response.data
}

export async function getSalesTrend(
  range: DateRange,
  groupBy: 'day' | 'month',
): Promise<SalesTrendPoint[]> {
  const response = await api.get<SalesTrendPoint[]>(
    '/owner/dashboard/sales-trend',
    { params: { ...range, groupBy } },
  )
  return response.data
}

export async function getTopProducts(
  range: DateRange,
  limit = 5,
): Promise<TopProduct[]> {
  const response = await api.get<TopProduct[]>('/owner/dashboard/top-products', {
    params: { ...range, limit },
  })
  return response.data
}

export async function getPaymentMethods(
  range: DateRange,
): Promise<PaymentMethodBreakdown[]> {
  const response = await api.get<PaymentMethodBreakdown[]>(
    '/owner/dashboard/payment-methods',
    { params: range },
  )
  return response.data
}

export async function getExpenses(params: {
  from: string
  to: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<Expense>> {
  const response = await api.get<PagedResult<Expense>>('/owner/expenses', {
    params,
  })
  return response.data
}

export async function createExpense(input: ExpenseInput): Promise<Expense> {
  const response = await api.post<Expense>('/owner/expenses', input)
  return response.data
}

export async function updateExpense(
  id: string,
  input: ExpenseInput,
): Promise<Expense> {
  const response = await api.put<Expense>(`/owner/expenses/${id}`, input)
  return response.data
}

export async function deleteExpense(id: string): Promise<void> {
  await api.delete(`/owner/expenses/${id}`)
}

export async function getIncomes(params: {
  from: string
  to: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<Income>> {
  const response = await api.get<PagedResult<Income>>('/owner/incomes', {
    params,
  })
  return response.data
}

export async function createManualIncome(
  input: ManualIncomeInput,
): Promise<Income> {
  const response = await api.post<Income>('/owner/incomes', input)
  return response.data
}

export async function deleteIncome(id: string): Promise<void> {
  await api.delete(`/owner/incomes/${id}`)
}

export async function getReportData(range: DateRange): Promise<ReportData> {
  const response = await api.get<ReportData>('/owner/reports/data', {
    params: range,
  })
  return response.data
}

export async function downloadReport(
  range: DateRange,
  format: ReportFormat,
  type: ReportType,
): Promise<void> {
  const response = await api.get<Blob>('/owner/reports/export', {
    params: { ...range, format, type },
    responseType: 'blob',
  })

  const disposition = response.headers['content-disposition'] as
    | string
    | undefined
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
  const fileName = match
    ? decodeURIComponent(match[1])
    : `Laporan-Hanora-${range.from}_${range.to}.${format}`

  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
