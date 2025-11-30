import './Navigation.css';

function Navigation() {
  return (
    <nav className="navigation">
      <div className="container">
        <ul className="nav-list">
          <li><a href="#home">Главная</a></li>
          <li><a href="#catalog">Каталог</a></li>
          <li><a href="#about">О нас</a></li>
          <li><a href="#contact">Контакты</a></li>
        </ul>
      </div>
    </nav>
  );
}

export default Navigation;