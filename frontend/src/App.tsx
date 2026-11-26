import { lazy, Suspense, type ReactNode } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { Layout } from './components/Layout'
import { Loader } from './components/Loader'
import { SmoothScroll } from './motion/SmoothScroll'
import { HomePage } from './pages/home/HomePage'

// Pages chargées à la demande : la page d'accueil reste légère sur réseau mobile.
const ExplorePage = lazy(() => import('./pages/listings/ExplorePage'))
const ListingPage = lazy(() => import('./pages/listings/ListingPage'))
const ListingEditorPage = lazy(() => import('./pages/listings/ListingEditorPage'))
const MyListingsPage = lazy(() => import('./pages/listings/MyListingsPage'))
const ExchangesPage = lazy(() => import('./pages/exchanges/ExchangesPage'))
const ExchangePage = lazy(() => import('./pages/exchanges/ExchangePage'))
const TimeAccountPage = lazy(() => import('./pages/exchanges/TimeAccountPage'))
const ProfilePage = lazy(() => import('./pages/account/ProfilePage'))
const MemberPage = lazy(() => import('./pages/account/MemberPage'))
const LoginPage = lazy(() => import('./pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('./pages/auth/RegisterPage'))
const ConfirmEmailPage = lazy(() => import('./pages/auth/ConfirmEmailPage'))
const ForgotPasswordPage = lazy(() => import('./pages/auth/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('./pages/auth/ResetPasswordPage'))
const HowItWorksPage = lazy(() => import('./pages/info/HowItWorksPage'))
const RulesPage = lazy(() => import('./pages/info/RulesPage'))
const NotFoundPage = lazy(() => import('./pages/info/NotFoundPage'))

const member = (page: ReactNode) => <RequireAuth>{page}</RequireAuth>

/** Racine de l'application : session, défilement amorti et routes en français. */
export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <SmoothScroll />
        <Suspense fallback={<Loader />}>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<HomePage />} />
              <Route path="annonces" element={<ExplorePage />} />
              <Route path="annonces/:id" element={<ListingPage />} />
              <Route path="membres/:id" element={<MemberPage />} />
              <Route path="publier" element={member(<ListingEditorPage />)} />
              <Route path="mes-annonces" element={member(<MyListingsPage />)} />
              <Route path="mes-annonces/:id/modifier" element={member(<ListingEditorPage />)} />
              <Route path="echanges" element={member(<ExchangesPage />)} />
              <Route path="echanges/:id" element={member(<ExchangePage />)} />
              <Route path="temps" element={member(<TimeAccountPage />)} />
              <Route path="moi" element={member(<ProfilePage />)} />
              <Route path="connexion" element={<LoginPage />} />
              <Route path="inscription" element={<RegisterPage />} />
              <Route path="confirmer-email" element={<ConfirmEmailPage />} />
              <Route path="mot-de-passe-oublie" element={<ForgotPasswordPage />} />
              <Route path="reinitialiser-mot-de-passe" element={<ResetPasswordPage />} />
              <Route path="comment-ca-marche" element={<HowItWorksPage />} />
              <Route path="regles" element={<RulesPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
    </BrowserRouter>
  )
}
