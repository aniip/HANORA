import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { RequireAuth } from '../components/auth/RequireAuth'
import { RequireTableSession } from '../components/customer/RequireTableSession'
import { CashierLayout } from '../components/layout/CashierLayout'
import { CustomerLayout } from '../components/layout/CustomerLayout'
import { OwnerLayout } from '../components/layout/OwnerLayout'
import { PublicLayout } from '../components/layout/PublicLayout'
import { CashierHistoryPage } from '../pages/CashierHistoryPage'
import { CashierQueuePage } from '../pages/CashierQueuePage'
import { ChangePasswordPage } from '../pages/ChangePasswordPage'
import { CheckoutPage } from '../pages/CheckoutPage'
import { ForbiddenPage } from '../pages/ForbiddenPage'
import { GalleryPage } from '../pages/GalleryPage'
import { HomePage } from '../pages/HomePage'
import { LocationPage } from '../pages/LocationPage'
import { LoginPage } from '../pages/LoginPage'
import { MenuPage } from '../pages/MenuPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { OrderMenuPage } from '../pages/OrderMenuPage'
import { OrderTrackingPage } from '../pages/OrderTrackingPage'
import { PaymentSimulationPage } from '../pages/PaymentSimulationPage'
import { TableLandingPage } from '../pages/TableLandingPage'
import { CategoryManagementPage } from '../pages/owner/CategoryManagementPage'
import { ExpenseManagementPage } from '../pages/owner/ExpenseManagementPage'
import { ProductManagementPage } from '../pages/owner/ProductManagementPage'
import { SettingsPage } from '../pages/owner/SettingsPage'
import { TableManagementPage } from '../pages/owner/TableManagementPage'
import { UserManagementPage } from '../pages/owner/UserManagementPage'

// Grafik owner memuat Recharts; pisahkan agar halaman publik tetap ringan.
const OwnerDashboardPage = lazy(() =>
  import('../pages/owner/OwnerDashboardPage').then((module) => ({
    default: module.OwnerDashboardPage,
  })),
)

const ReportPage = lazy(() =>
  import('../pages/owner/ReportPage').then((module) => ({
    default: module.ReportPage,
  })),
)

export function AppRouter() {
  return (
    <Suspense
      fallback={
        <div className="grid min-h-[40vh] place-items-center text-sm text-coffee-700">
          Memuat halaman...
        </div>
      }
    >
      <Routes>
      <Route element={<PublicLayout />}>
        <Route index element={<HomePage />} />
        <Route path="menu" element={<MenuPage />} />
        <Route path="galeri" element={<GalleryPage />} />
        <Route path="lokasi" element={<LocationPage />} />

        <Route path="masuk" element={<LoginPage />} />

        <Route
          path="ganti-password"
          element={
            <RequireAuth>
              <ChangePasswordPage />
            </RequireAuth>
          }
        />

        <Route path="403" element={<ForbiddenPage />} />

        <Route
          path="t/:qrToken"
          element={<TableLandingPage />}
        />

        <Route
          path="pesanan/:trackingToken"
          element={<OrderTrackingPage />}
        />

        <Route
          path="pesanan/:trackingToken/simulasi"
          element={<PaymentSimulationPage />}
        />

        <Route path="*" element={<NotFoundPage />} />
      </Route>

      <Route element={<CustomerLayout />}>
        <Route
          path="pesan"
          element={
            <RequireTableSession>
              <OrderMenuPage />
            </RequireTableSession>
          }
        />

        <Route
          path="checkout"
          element={
            <RequireTableSession>
              <CheckoutPage />
            </RequireTableSession>
          }
        />
      </Route>

      <Route
        path="kasir"
        element={
          <RequireAuth roles={['Owner', 'Cashier']}>
            <CashierLayout />
          </RequireAuth>
        }
      >
        <Route index element={<CashierQueuePage />} />
        <Route path="riwayat" element={<CashierHistoryPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>

      <Route
        path="owner"
        element={
          <RequireAuth roles={['Owner']}>
            <OwnerLayout />
          </RequireAuth>
        }
      >
        <Route index element={<OwnerDashboardPage />} />
        <Route path="produk" element={<ProductManagementPage />} />
        <Route path="kategori" element={<CategoryManagementPage />} />
        <Route path="meja" element={<TableManagementPage />} />
        <Route path="pengeluaran" element={<ExpenseManagementPage />} />
        <Route path="laporan" element={<ReportPage />} />
        <Route path="pengguna" element={<UserManagementPage />} />
        <Route path="pengaturan" element={<SettingsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      </Routes>
    </Suspense>
  )
}
