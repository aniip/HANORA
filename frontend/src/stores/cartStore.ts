import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { Product } from '../lib/catalog'

export type CartItem = {
  productId: string
  name: string
  price: number
  imageUrl: string | null
  quantity: number
}

type CartState = {
  tableId: string | null
  items: CartItem[]
  setTable: (tableId: string) => void
  add: (product: Product) => void
  increment: (productId: string) => void
  decrement: (productId: string) => void
  remove: (productId: string) => void
  clear: () => void
}

export const useCartStore = create<CartState>()(
  persist(
    (set) => ({
      tableId: null,
      items: [],
      setTable: (tableId) =>
        set((state) =>
          state.tableId === tableId
            ? state
            : { tableId, items: [] },
        ),
      add: (product) =>
        set((state) => {
          const existing = state.items.find(
            (item) => item.productId === product.id,
          )

          if (existing) {
            return {
              items: state.items.map((item) =>
                item.productId === product.id
                  ? { ...item, quantity: item.quantity + 1 }
                  : item,
              ),
            }
          }

          return {
            items: [
              ...state.items,
              {
                productId: product.id,
                name: product.name,
                price: product.price,
                imageUrl: product.imageUrl,
                quantity: 1,
              },
            ],
          }
        }),
      increment: (productId) =>
        set((state) => ({
          items: state.items.map((item) =>
            item.productId === productId
              ? { ...item, quantity: item.quantity + 1 }
              : item,
          ),
        })),
      decrement: (productId) =>
        set((state) => ({
          items: state.items
            .map((item) =>
              item.productId === productId
                ? { ...item, quantity: item.quantity - 1 }
                : item,
            )
            .filter((item) => item.quantity > 0),
        })),
      remove: (productId) =>
        set((state) => ({
          items: state.items.filter((item) => item.productId !== productId),
        })),
      clear: () => set({ items: [] }),
    }),
    {
      name: 'hanora-cart',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({
        tableId: state.tableId,
        items: state.items,
      }),
    },
  ),
)
