import React, { useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import './App.css';

// Компоненты
import Header from './components/Layout/Header';
import Login from './components/Auth/Login';
import Register from './components/Auth/Register';
import UserProfile from './components/User/UserProfile';
import SavedProjects from './components/User/SavedProjects';
import MapApp from './components/MapApp/MapApp';

// Защищенный маршрут
const PrivateRoute = ({ children }) => {
  const { isAuthenticated, loading } = useAuth();
  
  if (loading) {
    return <div className="loading-screen">Загрузка...</div>;
  }
  
  return isAuthenticated ? children : <Navigate to="/login" />;
};

// Главный лэйаут
const MainLayout = ({ children }) => {
  return (
    <div className="app">
      <Header />
      <main className="main-content">
        {children}
      </main>
    </div>
  );
};

// Компонент для страниц авторизации
const AuthPage = ({ children }) => {
  const { isAuthenticated } = useAuth();
  
  if (isAuthenticated) {
    return <Navigate to="/" />;
  }
  
  return <div className="auth-page">{children}</div>;
};

// Компонент переключения между логином и регистрацией
const AuthSwitcher = () => {
  const [isLogin, setIsLogin] = useState(true);
  
  return (
    <AuthPage>
      {isLogin ? (
        <Login onSwitchToRegister={() => setIsLogin(false)} />
      ) : (
        <Register onSwitchToLogin={() => setIsLogin(true)} />
      )}
    </AuthPage>
  );
};

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          {/* Публичные маршруты */}
          <Route path="/login" element={<AuthSwitcher />} />
          <Route path="/register" element={<AuthSwitcher />} />
          
          {/* Защищенные маршруты */}
          <Route path="/" element={
            <PrivateRoute>
              <MainLayout>
                <MapApp />
              </MainLayout>
            </PrivateRoute>
          } />
          
          <Route path="/profile" element={
            <PrivateRoute>
              <MainLayout>
                <UserProfile />
              </MainLayout>
            </PrivateRoute>
          } />
          
          <Route path="/projects" element={
            <PrivateRoute>
              <MainLayout>
                <SavedProjects />
              </MainLayout>
            </PrivateRoute>
          } />
          
          {/* Резервный маршрут */}
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
}

export default App;