import React from 'react';
import Header from './Header';
import './Layout.css';

const Layout = ({ children }) => {
  return (
    <div className="layout">
      <Header />
      <main className="layout-content">
        {children}
      </main>
      <footer className="layout-footer">
        <div className="container">
          <div className="footer-content">
            <div className="footer-section">
              <h4> Система проектирования зданий</h4>
              <p>Профессиональный инструмент для архитекторов и проектировщиков</p>
            </div>
            <div className="footer-section">
              <h5>Контакты</h5>
              <ul>
                <li>Email: support@building-design.ru</li>
                <li>Телефон: +7 (999) 123-45-67</li>
                <li>Адрес: г. Москва, ул. Проектная, д. 1</li>
              </ul>
            </div>
            <div className="footer-section">
              <h5>Быстрые ссылки</h5>
              <ul>
                <li><a href="/">Главная</a></li>
                <li><a href="/projects">Мои проекты</a></li>
                <li><a href="/profile">Профиль</a></li>
                <li><a href="/help">Помощь</a></li>
              </ul>
            </div>
          </div>
          <div className="footer-bottom">
            <p>© 2024 Система проектирования зданий. Все права защищены.</p>
            <div className="footer-links">
              <a href="/privacy">Политика конфиденциальности</a>
              <a href="/terms">Условия использования</a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
};

export default Layout;