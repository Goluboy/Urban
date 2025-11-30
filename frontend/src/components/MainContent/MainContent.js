import './MainContent.css';
import MapboxExample from '../Map/MapComponent';
import GenerateBuildingsButton from '../Map/GenerateBuildingsButton';

function MainContent() {
  const categories = [
    { id: 1, name: 'Электроника', items: 124 },
    { id: 2, name: 'Одежда', items: 89 },
    { id: 3, name: 'Книги', items: 256 },
    { id: 4, name: 'Спорт', items: 67 }
  ];

  return (
    <main className="main-content">
      <div className="container">
        <section className="hero">
          <h2>Добро пожаловать в наш магазин!</h2>
          <p>Мы предлагаем широкий ассортимент товаров на любой вкус</p>
        </section>

        <section className="categories">
          <h3>Категории товаров</h3>
          <div className="categories-grid">
            {categories.map(category => (
              <div key={category.id} className="category-card">
                <h4>{category.name}</h4>
                <p>{category.items} товаров</p>
                <button className="btn">Смотреть</button>
              </div>
            ))}
          </div>
        </section>
        <GenerateBuildingsButton />
        {/* ДОБАВЛЯЕМ КАРТУ */}
        <MapboxExample />
      </div>
    </main>
  );
}

export default MainContent;