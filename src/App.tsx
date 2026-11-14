import { AppRouter } from './app/AppRouter'
import { AuthBootstrap } from './components/layout/AuthBootstrap'

export default function App() {
  return (
    <AuthBootstrap>
      <AppRouter />
    </AuthBootstrap>
  )
}
