import { useState } from 'react'; // Добавляем импорт
import './Sidebar.css';

const Sidebar = ({ 
  isDrawingPolygon, 
  polygonPoints, 
  hasFinalPolygon,
  getPointsText,
  onStartDrawing, 
  onAcceptPolygon,
  onRemoveLastPoint,
  onClearPolygon,
  onGenerateBuildings,
  layouts,
  selectedLayout,
  onSelectLayout,
  isLoading
}) => {
  const [maxFloors, setMaxFloors] = useState('');
  const [grossFloorArea, setGrossFloorArea] = useState('');

  return (
    <div className="sidebar">
      <h4>Управление участком</h4>
      
      <div className="controls-row">
        <button 
          className={`btn ${isDrawingPolygon ? 'btn-cancel' : 'btn-primary'}`}
          onClick={onStartDrawing}
          disabled={isLoading}
        >
          {isDrawingPolygon ? 'Отменить' : 'Нарисовать участок'}
        </button>
      </div>
      
      {isDrawingPolygon && polygonPoints.length > 0 && (
        <div className="controls-row">
          <button 
            className="btn btn-warning"
            onClick={onRemoveLastPoint}
            disabled={polygonPoints.length === 0 || isLoading}
          >
            Удалить последнюю точку
          </button>
        </div>
      )}
      
      {polygonPoints.length >= 3 && isDrawingPolygon && (
        <div className="controls-row">
          <button 
            className="btn btn-success"
            onClick={onAcceptPolygon}
            disabled={isLoading}
          >
            Завершить участок
          </button>
          <div className="points-info">
            {polygonPoints.length} {getPointsText(polygonPoints.length)}
          </div>
        </div>
      )}
      
      {hasFinalPolygon && (
        <button 
          className="btn btn-danger full-width"
          onClick={onClearPolygon}
          disabled={isLoading}
        >
          Очистить участок
        </button>
      )}
      
      <div className="divider"></div>
      
      <h4>Параметры застройки</h4>
      <div className="params">
        <div className="param-group">
          <label>Макс. этажность:</label>
          <input 
            type="number" 
            placeholder="Не ограничено"
            min="1" 
            max="50"
            value={maxFloors}
            onChange={(e) => setMaxFloors(e.target.value)}
            disabled={isLoading}
          />
        </div>
        <div className="param-group">
          <label>Общая площадь (м²):</label>
          <input 
            type="number" 
            placeholder="Не ограничено" 
            min="1000" 
            step="1000"
            value={grossFloorArea}
            onChange={(e) => setGrossFloorArea(e.target.value)}
            disabled={isLoading}
          />
        </div>
      </div>
      
      <button 
        className="btn btn-generate full-width"
        onClick={() => onGenerateBuildings(maxFloors || null, grossFloorArea || null)}
        disabled={!hasFinalPolygon || isLoading}
      >
        {isLoading ? (
          <>
            <span className="spinner"></span>
            Генерация...
          </>
        ) : 'Сгенерировать здания'}
      </button>
      
      {layouts.length > 0 && (
        <>
          <div className="divider"></div>
          <h4>Концепции застройки</h4>
          <div className="layouts-list">
            {layouts.map((layout, index) => (
              <div 
                key={index}
                className={`layout-item ${selectedLayout === layout.name ? 'selected' : ''}`}
                onClick={() => onSelectLayout(layout.name)}
              >
                <div className="layout-header">
                  <span className="layout-name">{layout.name}</span>
                  <span className="layout-count">{layout.sections?.length || 0} здания</span>
                </div>
                <div className="layout-info">
                  {layout.sections?.map((section, i) => (
                    <div key={i} className="section-info">
                      {section.floors} эт. ({section.height} м)
                    </div>
                  )) || <div className="section-info">Нет данных</div>}
                </div>
              </div>
            ))}
          </div>
        </>
      )}
      
      <div className="divider"></div>
      
      <div className="status-info">
        <h5>Статус:</h5>
        <div className="status-text">
          {isLoading ? (
            <span className="loading">⏳ Генерация зданий...</span>
          ) : hasFinalPolygon ? (
            <span className="success">✅ Участок выбран</span>
          ) : isDrawingPolygon ? (
            <span className="drawing">✏️ Рисуется участок: {polygonPoints.length} {getPointsText(polygonPoints.length)}</span>
          ) : (
            <span className="waiting">⏳ Участок не выбран</span>
          )}
        </div>
      </div>
    </div>
  );
};

export default Sidebar;