import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import './Layout.css';

const Header = () => {
  const { user, logout, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [dropdownOpen, setDropdownOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
    setDropdownOpen(false);
  };

  const toggleDropdown = () => {
    setDropdownOpen(!dropdownOpen);
  };

  return (
    <header className="header">
      <div className="header-container">
        <div className="logo">
          <Link to="/" className="logo-link">
            <div className="logo-icon"></div>
            <div className="logo-text">
              <h1>BuildingDesign</h1>
              <p className="logo-subtitle">Проектирование зданий</p>
            </div>
          </Link>
        </div>
        
        {/* Навигация оставлена только с Главной */}
        <nav className="nav">
          <ul className="nav-menu">
            <li className="nav-item">
              <Link to="/" className="nav-link">
                <i className="fas fa-home"></i>
                <span>Главная</span>
              </Link>
            </li>
            {/* Убраны: Проекты, Шаблоны, Помощь */}
          </ul>
        </nav>
        
        <div className="header-actions">
          {isAuthenticated ? (
            <div className="user-profile">
              <div 
                className="user-info" 
                onClick={toggleDropdown}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    toggleDropdown();
                  }
                }}
              >
                <div className="user-avatar">
                  {user?.name?.charAt(0).toUpperCase() || 'U'}
                </div>
                <div className="user-details">
                  <span className="user-name">{user?.name || 'Пользователь'}</span>
                  {user?.company && (
                    <span className="user-company">{user.company}</span>
                  )}
                </div>
                <i className={`fas fa-chevron-${dropdownOpen ? 'up' : 'down'}`}></i>
              </div>
              
              {dropdownOpen && (
                <div className="dropdown-menu show">
                  <Link to="/profile" className="dropdown-item" onClick={() => setDropdownOpen(false)}>
                    <i className="fas fa-user"></i>
                    <span>Профиль</span>
                  </Link>
                  <Link to="/settings" className="dropdown-item" onClick={() => setDropdownOpen(false)}>
                    <i className="fas fa-cog"></i>
                    <span>Настройки</span>
                  </Link>
                  <div className="dropdown-divider"></div>
                  <button onClick={handleLogout} className="dropdown-item logout-btn">
                    <i className="fas fa-sign-out-alt"></i>
                    <span>Выйти</span>
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="auth-buttons">
              <Link to="/login" className="btn btn-outline">
                <i className="fas fa-sign-in-alt"></i>
                <span>Войти</span>
              </Link>
              <Link to="/register" className="btn btn-primary">
                <i className="fas fa-user-plus"></i>
                <span>Регистрация</span>
              </Link>
            </div>
          )}
        </div>
        
        <button className="mobile-menu-btn" aria-label="Меню">
          <i className="fas fa-bars"></i>
        </button>
      </div>
    </header>
  );
};

export default Header;