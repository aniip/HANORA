import { api } from './api'
import { customerApi } from './customerApi'
import type { TableInfo } from './tables'

export type OrderStatus =
  | 'pending_payment'
  | 'pending_confirmation'
  | 'processing'
  | 'ready'
  | 'completed'
  | 'cancelled'

export type PaymentStatus =
  | 'pending'
  | 'paid'
  | 'failed'
  | 'expired'
  | 'refunded'
  | 'cancelled'

export type PaymentMethod = 'qris' | 'cash'

export type TableSessionResponse = {
  table: TableInfo
  tableSessionToken: string
  expiresIn: number
}

export type OrderItem = {
  productName: string
  unitPrice: number
  quantity: number
  subtotal: number
  notes: string | null
}

export type PaymentSummary = {
  method: PaymentMethod | 'unknown'
  provider: string
  status: PaymentStatus | 'unknown'
  paidAt: string | null
  expiresAt: string | null
  snapToken: string | null
  redirectUrl: string | null
  simulation: boolean
}

export type TrackingEvent = {
  status: OrderStatus
  at: string
  note: string | null
}

export type OrderResponse = {
  orderNumber: string
  status: OrderStatus
  customerName: string
  table: TableInfo
  items: OrderItem[]
  subtotal: number
  taxRate: number
  taxAmount: number
  discountAmount: number
  total: number
  trackingToken: string
  payment: PaymentSummary
  createdAt: string
}

export type OrderTracking = Omit<OrderResponse, 'trackingToken'> & {
  timeline: TrackingEvent[]
}

export type CreateOrderInput = {
  customerName: string
  paymentMethod: PaymentMethod
  notes?: string
  items: Array<{
    productId: string
    quantity: number
    notes?: string
  }>
}

export type CashierOrderSummary = {
  id: string
  orderNumber: string
  customerName: string
  table: TableInfo
  total: number
  status: OrderStatus
  paymentMethod: PaymentMethod | 'unknown'
  paymentStatus: PaymentStatus | 'unknown'
  itemCount: number
  createdAt: string
}

export type PagedCashierOrders = {
  items: CashierOrderSummary[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type CashierOrderDetail = {
  id: string
  orderNumber: string
  customerName: string
  table: TableInfo
  status: OrderStatus
  items: OrderItem[]
  subtotal: number
  taxRate: number
  taxAmount: number
  discountAmount: number
  total: number
  notes: string | null
  cashierName: string | null
  paidAt: string | null
  createdAt: string
  payment: PaymentSummary
  timeline: TrackingEvent[]
}

export type TransactionSummary = {
  orderNumber: string
  customerName: string
  table: TableInfo
  total: number
  method: PaymentMethod | 'unknown'
  status: PaymentStatus | 'unknown'
  paidAt: string | null
}

export type PagedTransactions = {
  items: TransactionSummary[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export async function resolveTable(
  qrToken: string,
): Promise<TableSessionResponse> {
  const response = await customerApi.get<TableSessionResponse>(
    `/public/tables/${qrToken}`,
  )

  return response.data
}

export async function createOrder(
  tableSessionToken: string,
  idempotencyKey: string,
  input: CreateOrderInput,
): Promise<OrderResponse> {
  const response = await customerApi.post<OrderResponse>('/orders', input, {
    headers: {
      Authorization: `Bearer ${tableSessionToken}`,
      'Idempotency-Key': idempotencyKey,
    },
  })

  return response.data
}

export async function trackOrder(
  trackingToken: string,
): Promise<OrderTracking> {
  const response = await customerApi.get<OrderTracking>(
    `/orders/track/${trackingToken}`,
  )

  return response.data
}

export async function cancelOrder(
  trackingToken: string,
  reason: string,
): Promise<OrderTracking> {
  const response = await customerApi.post<OrderTracking>(
    `/orders/track/${trackingToken}/cancel`,
    { reason },
  )

  return response.data
}

export async function initiateQrisPayment(
  trackingToken: string,
): Promise<PaymentSummary> {
  const response = await customerApi.post<PaymentSummary>(
    `/orders/track/${trackingToken}/payments/qris`,
  )

  return response.data
}

export async function simulateQrisPayment(
  trackingToken: string,
): Promise<OrderTracking> {
  const response = await customerApi.post<OrderTracking>(
    `/orders/track/${trackingToken}/simulate`,
  )

  return response.data
}

export async function getCashierOrders(params: {
  status?: string
  page?: number
  pageSize?: number
} = {}): Promise<PagedCashierOrders> {
  const response = await api.get<PagedCashierOrders>('/cashier/orders', {
    params,
  })

  return response.data
}

export async function getCashierOrder(id: string): Promise<CashierOrderDetail> {
  const response = await api.get<CashierOrderDetail>(`/cashier/orders/${id}`)
  return response.data
}

export async function updateCashierOrderStatus(
  id: string,
  toStatus: 'ready' | 'completed',
  note?: string,
): Promise<CashierOrderDetail> {
  const response = await api.patch<CashierOrderDetail>(
    `/cashier/orders/${id}/status`,
    { toStatus, note },
  )

  return response.data
}

export async function confirmCashPayment(
  id: string,
  note?: string,
): Promise<CashierOrderDetail> {
  const response = await api.post<CashierOrderDetail>(
    `/cashier/orders/${id}/confirm-cash`,
    { note },
  )

  return response.data
}

export async function cancelCashierOrder(
  id: string,
  reason: string,
): Promise<CashierOrderDetail> {
  const response = await api.post<CashierOrderDetail>(
    `/cashier/orders/${id}/cancel`,
    { reason },
  )

  return response.data
}

export async function getCashierTransactions(params: {
  from?: string
  to?: string
  method?: string
  status?: string
  page?: number
  pageSize?: number
} = {}): Promise<PagedTransactions> {
  const response = await api.get<PagedTransactions>('/cashier/transactions', {
    params,
  })

  return response.data
}
