import './Footer.css';

function Footer() {
  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-content">
          <div className="footer-section">
            <h4>Контакты</h4>
            <p>Email: info@myshop.com</p>
            <p>Телефон: +7 (999) 123-45-67</p>
          </div>
          <div className="footer-section">
            <h4>Мы в соцсетях</h4>
            <p>VK | Telegram | YouTube</p>
          </div>
          <div className="footer-section">
            <h4>Адрес</h4>
            <p>г. Москва, ул. Примерная, д. 123</p>
          </div>
        </div>
        <div className="footer-bottom">
          <p>&copy; 2024 Мой Интернет-Магазин. Все права защищены.</p>
        </div>
      </div>
    </footer>
  );
}

export default Footer;