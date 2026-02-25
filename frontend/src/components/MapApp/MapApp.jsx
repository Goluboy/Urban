import { useState, useRef, useEffect, useCallback } from 'react';
import mapboxgl from 'mapbox-gl';
import 'mapbox-gl/dist/mapbox-gl.css';
import './MapApp.css';
import Sidebar from './Sidebar';

const MAPBOX_TOKEN = '';

const getPointsText = (count) => {
  if (count === 1) return 'точка';
  if (count >= 2 && count <= 4) return 'точки';
  return 'точек';
};

const MapApp = () => {
  const mapContainer = useRef(null);
  const map = useRef(null);
  const markers = useRef([]);
  
  const [polygonPoints, setPolygonPoints] = useState([]);
  const [isDrawingPolygon, setIsDrawingPolygon] = useState(false);
  const [hasFinalPolygon, setHasFinalPolygon] = useState(false);
  const [is3DView, setIs3DView] = useState(true);
  const [layouts, setLayouts] = useState([]);
  const [selectedLayout, setSelectedLayout] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  // Демо-данные из задания
  const demoLayouts = [
    {
      name: "Концепция 1",
      sections: [
        {
          polygon: [[[60.60762292926969,56.83848907900047],[60.60723734977997,56.83844665928913],[60.6073167207697,56.83822992326984],[60.60770229813374,56.838272342719826],[60.60762292926969,56.83848907900047]]],
          floors: 17,
          height: 54.4,
          appartmetsArea: 8034,
          commercialArea: 0
        },
        {
          polygon: [[[60.606573917753856,56.838364131030026],[60.60618834082044,56.838321708097325],[60.60626592657696,56.838109863062996],[60.60665150143272,56.83815228574022],[60.606573917753856,56.838364131030026]]],
          floors: 24,
          height: 76.8,
          appartmetsArea: 11086,
          commercialArea: 0
        }
      ]
    },
    {
      name: "Концепция 2",
      sections: [
        {
          polygon: [[[60.60748626215009,56.83847793699064],[60.60710068294074,56.838435516859214],[60.607183735737,56.83820872925014],[60.6075693127221,56.838251149108075],[60.60748626215009,56.83847793699064]]],
          floors: 12,
          height: 38.4,
          appartmetsArea: 5439,
          commercialArea: 464
        }
      ]
    },
    {
      name: "Концепция 3",
      sections: [
        {
          polygon: [[[60.60724874850933,56.83822019499104],[60.607179194440235,56.83843234995155],[60.60670127631295,56.83838527804908],[60.60677083298438,56.83817312337485],[60.60724874850933,56.83822019499104]]],
          floors: 13,
          height: 41.6,
          appartmetsArea: 7396,
          commercialArea: 0
        },
        {
          polygon: [[[60.60617475074121,56.83833416822256],[60.60764912664992,56.83849637897038],[60.60773595202454,56.83826930289016],[60.606234439217445,56.83810413025229],[60.60617475074121,56.83833416822256]]],
          floors: 3,
          height: 9.6,
          appartmetsArea: 5741,
          commercialArea: 0
        }
      ]
    }
  ];

  // Очистка всех слоев зданий
  const clearBuildingLayers = useCallback(() => {
    if (!map.current) return;
    
    // Удаляем все слои, которые начинаются с 'building-'
    const layers = map.current.getStyle().layers || [];
    const sourcesToRemove = new Set();
    
    layers.forEach(layer => {
      if (layer.id && layer.id.startsWith('building-')) {
        if (map.current.getLayer(layer.id)) {
          map.current.removeLayer(layer.id);
        }
        if (layer.source) {
          sourcesToRemove.add(layer.source);
        }
      }
    });
    
    // Удаляем источники
    sourcesToRemove.forEach(sourceId => {
      if (map.current.getSource(sourceId)) {
        map.current.removeSource(sourceId);
      }
    });
  }, []);

  // Очистка ВСЕХ временных слоев (участок + здания)
  const clearAllMapLayers = useCallback(() => {
    if (!map.current) return;
    
    // Удаляем слои участка
    const tempLayers = ['temp-line', 'final-polygon', 'final-polygon-outline'];
    tempLayers.forEach(layerId => {
      if (map.current.getLayer(layerId)) {
        map.current.removeLayer(layerId);
      }
    });
    
    // Удаляем источники участка
    const tempSources = ['temp-line', 'final-polygon'];
    tempSources.forEach(sourceId => {
      if (map.current.getSource(sourceId)) {
        map.current.removeSource(sourceId);
      }
    });
    
    // Удаляем слои зданий
    clearBuildingLayers();
  }, [clearBuildingLayers]);

  const update3DBuildings = useCallback(() => {
    if (!map.current) return;
    
    if (map.current.getLayer('3d-buildings')) {
      map.current.removeLayer('3d-buildings');
    }
    
    if (is3DView) {
      map.current.addLayer({
        'id': '3d-buildings',
        'source': 'composite',
        'source-layer': 'building',
        'filter': ['==', 'extrude', 'true'],
        'type': 'fill-extrusion',
        'minzoom': 15,
        'paint': {
          'fill-extrusion-color': '#aaa',
          'fill-extrusion-height': [
            'interpolate',
            ['linear'],
            ['zoom'],
            15, 0,
            15.05, ['get', 'height']
          ],
          'fill-extrusion-base': [
            'interpolate',
            ['linear'],
            ['zoom'],
            15, 0,
            15.05, ['get', 'min_height']
          ],
          'fill-extrusion-opacity': 0.6
        }
      }, 'waterway-label');
    }
  }, [is3DView]);

  useEffect(() => {
    if (map.current) return;

    if (!MAPBOX_TOKEN || MAPBOX_TOKEN === 'YOUR_MAPBOX_TOKEN_HERE') {
      console.error('Mapbox token не установлен!');
      alert('Пожалуйста, установите ваш Mapbox токен в файле MapApp.jsx');
      return;
    }

    mapboxgl.accessToken = MAPBOX_TOKEN;
    
    map.current = new mapboxgl.Map({
      container: mapContainer.current,
      style: 'mapbox://styles/mapbox/streets-v11',
      center: [60.6057, 56.8389],
      zoom: 13,
      pitch: is3DView ? 45 : 0,
      bearing: is3DView ? -17.6 : 0,
      antialias: true
    });

    map.current.addControl(new mapboxgl.NavigationControl(), 'top-right');
    map.current.addControl(new mapboxgl.FullscreenControl(), 'top-right');
    map.current.addControl(new mapboxgl.ScaleControl(), 'bottom-left');

    map.current.on('load', () => {
      update3DBuildings();
    });

    return () => {
      if (map.current) {
        map.current.remove();
        map.current = null;
      }
    };
  }, [is3DView, update3DBuildings]);

  useEffect(() => {
    if (map.current && map.current.isStyleLoaded()) {
      update3DBuildings();
    }
  }, [is3DView, update3DBuildings]);

  const getBuildingColor = (floors) => {
    if (floors <= 5) return '#4ade80'; // зеленый
    if (floors <= 15) return '#fbbf24'; // желтый
    return '#ef4444'; // красный
  };

  // Отрисовка зданий на карте
  const renderBuildingsOnMap = useCallback((sections, layoutName) => {
    if (!map.current || !sections || !map.current.isStyleLoaded()) {
      console.log('Карта не готова для отрисовки');
      return;
    }
    
    // Сначала очищаем старые слои зданий
    clearBuildingLayers();
    
    sections.forEach((section, index) => {
      const sourceId = `building-${layoutName}-${index}`;
      const fillLayerId = `${sourceId}-fill`;
      const outlineLayerId = `${sourceId}-outline`;
      const extrusionLayerId = `${sourceId}-3d`;
      
      try {
        // Проверяем, существует ли уже источник с таким ID
        if (map.current.getSource(sourceId)) {
          console.log(`Источник ${sourceId} уже существует, пропускаем`);
          return;
        }
        
        // Создаем GeoJSON полигон
        const polygonFeature = {
          type: 'Feature',
          properties: {
            floors: section.floors || 1,
            height: section.height || 3,
            area: (section.appartmetsArea || 0) + (section.commercialArea || 0),
            name: `Секция ${index + 1}`
          },
          geometry: {
            type: 'Polygon',
            coordinates: section.polygon
          }
        };
        
        // Добавляем источник
        map.current.addSource(sourceId, {
          type: 'geojson',
          data: polygonFeature
        });
        
        // Добавляем слой заливки (2D)
        map.current.addLayer({
          id: fillLayerId,
          type: 'fill',
          source: sourceId,
          paint: {
            'fill-color': getBuildingColor(section.floors || 1),
            'fill-opacity': 0.7
          }
        });
        
        // Добавляем контур
        map.current.addLayer({
          id: outlineLayerId,
          type: 'line',
          source: sourceId,
          paint: {
            'line-color': '#1d4ed8',
            'line-width': 2
          }
        });
        
        // Если 3D вид, добавляем экструзию
        if (is3DView) {
          map.current.addLayer({
            id: extrusionLayerId,
            type: 'fill-extrusion',
            source: sourceId,
            paint: {
              'fill-extrusion-color': getBuildingColor(section.floors || 1),
              'fill-extrusion-height': section.height || 10,
              'fill-extrusion-base': 0,
              'fill-extrusion-opacity': 0.8
            }
          });
        }
        
        console.log(`Добавлено здание ${index + 1} из концепции ${layoutName}`);
        
      } catch (error) {
        console.error(`Ошибка при добавлении здания ${index + 1}:`, error);
      }
    });
  }, [is3DView, clearBuildingLayers]);

  const toggle3DView = useCallback(() => {
    if (!map.current) return;
    
    setIs3DView(prev => {
      const newIs3D = !prev;
      
      map.current.easeTo({
        pitch: newIs3D ? 45 : 0,
        bearing: newIs3D ? -17.6 : 0,
        duration: 1000
      });
      
      // Перерисовываем здания при переключении режима
      if (selectedLayout) {
        setTimeout(() => {
          const layout = layouts.find(l => l.name === selectedLayout);
          if (layout && layout.sections) {
            renderBuildingsOnMap(layout.sections, layout.name);
          }
        }, 500);
      }
      
      return newIs3D;
    });
  }, [selectedLayout, layouts, renderBuildingsOnMap]);

  const drawTempPolygon = useCallback((points) => {
    if (!map.current || points.length < 2) return;
    
    const lineString = {
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'LineString',
        coordinates: points.map(p => [p.lng, p.lat])
      }
    };

    // Удаляем старый временный слой
    if (map.current.getLayer('temp-line')) {
      map.current.removeLayer('temp-line');
    }
    if (map.current.getSource('temp-line')) {
      map.current.removeSource('temp-line');
    }

    map.current.addSource('temp-line', {
      type: 'geojson',
      data: lineString
    });

    map.current.addLayer({
      id: 'temp-line',
      type: 'line',
      source: 'temp-line',
      layout: {
        'line-cap': 'round',
        'line-join': 'round'
      },
      paint: {
        'line-color': '#3b82f6',
        'line-width': 3,
        'line-dasharray': [2, 2]
      }
    });
  }, []);

  const drawFinalPolygon = useCallback(() => {
    if (!map.current || polygonPoints.length < 3) return;
    
    const closedPoints = [...polygonPoints, polygonPoints[0]];
    
    // Удаляем старые слои участка
    if (map.current.getLayer('final-polygon')) {
      map.current.removeLayer('final-polygon');
    }
    if (map.current.getSource('final-polygon')) {
      map.current.removeSource('final-polygon');
    }
    if (map.current.getLayer('final-polygon-outline')) {
      map.current.removeLayer('final-polygon-outline');
    }
    
    const polygonFeature = {
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'Polygon',
        coordinates: [closedPoints.map(p => [p.lng, p.lat])]
      }
    };
    
    map.current.addSource('final-polygon', {
      type: 'geojson',
      data: polygonFeature
    });

    map.current.addLayer({
      id: 'final-polygon',
      type: 'fill',
      source: 'final-polygon',
      paint: {
        'fill-color': '#3b82f6',
        'fill-opacity': 0.3
      }
    });

    map.current.addLayer({
      id: 'final-polygon-outline',
      type: 'line',
      source: 'final-polygon',
      paint: {
        'line-color': '#1d4ed8',
        'line-width': 3
      }
    });
  }, [polygonPoints]);

  const addPoint = useCallback((lng, lat) => {
    if (!map.current) return;
    
    const newPoints = [...polygonPoints, { lng, lat }];
    setPolygonPoints(newPoints);
    
    // Очищаем временную линию
    if (map.current.getLayer('temp-line')) {
      map.current.removeLayer('temp-line');
    }
    if (map.current.getSource('temp-line')) {
      map.current.removeSource('temp-line');
    }
    
    const marker = new mapboxgl.Marker({ 
      color: '#3b82f6',
      draggable: true 
    })
      .setLngLat([lng, lat])
      .addTo(map.current);
    
    markers.current.push(marker);
    
    marker.on('dragend', () => {
      const newLngLat = marker.getLngLat();
      const index = markers.current.indexOf(marker);
      const updatedPoints = [...polygonPoints];
      updatedPoints[index] = { lng: newLngLat.lng, lat: newLngLat.lat };
      setPolygonPoints(updatedPoints);
      drawTempPolygon(updatedPoints);
    });
    
    if (newPoints.length > 1) {
      drawTempPolygon(newPoints);
    }
  }, [polygonPoints, drawTempPolygon]);

  useEffect(() => {
    if (!map.current) return;

    const handleMapClick = (e) => {
      if (!isDrawingPolygon) return;
      
      const { lng, lat } = e.lngLat;
      addPoint(lng, lat);
    };

    if (isDrawingPolygon) {
      map.current.on('click', handleMapClick);
      map.current.getCanvas().style.cursor = 'crosshair';
    } else {
      map.current.off('click', handleMapClick);
      map.current.getCanvas().style.cursor = '';
    }

    return () => {
      if (map.current) {
        map.current.off('click', handleMapClick);
      }
    };
  }, [isDrawingPolygon, addPoint]);

  const removeLastPoint = useCallback(() => {
    if (polygonPoints.length === 0) return;
    
    const lastMarker = markers.current.pop();
    if (lastMarker) lastMarker.remove();
    
    const newPoints = polygonPoints.slice(0, -1);
    setPolygonPoints(newPoints);
    
    // Очищаем временную линию
    if (map.current.getLayer('temp-line')) {
      map.current.removeLayer('temp-line');
    }
    if (map.current.getSource('temp-line')) {
      map.current.removeSource('temp-line');
    }
    
    if (newPoints.length > 1) {
      drawTempPolygon(newPoints);
    }
  }, [polygonPoints, drawTempPolygon]);

  const startDrawingPolygon = () => {
    if (isDrawingPolygon) {
      cancelDrawing();
    } else {
      setIsDrawingPolygon(true);
      setPolygonPoints([]);
      
      // Очищаем все маркеры
      markers.current.forEach(marker => marker.remove());
      markers.current = [];
      
      // Очищаем все слои на карте
      clearAllMapLayers();
      
      setHasFinalPolygon(false);
      setLayouts([]);
      setSelectedLayout(null);
    }
  };

  const cancelDrawing = useCallback(() => {
    setIsDrawingPolygon(false);
    setPolygonPoints([]);
    
    // Очищаем маркеры
    markers.current.forEach(marker => marker.remove());
    markers.current = [];
    
    // Очищаем временные слои
    if (map.current) {
      if (map.current.getLayer('temp-line')) {
        map.current.removeLayer('temp-line');
      }
      if (map.current.getSource('temp-line')) {
        map.current.removeSource('temp-line');
      }
    }
  }, []);

  const clearPolygon = useCallback(() => {
    setIsDrawingPolygon(false);
    setPolygonPoints([]);
    
    // Очищаем все маркеры
    markers.current.forEach(marker => marker.remove());
    markers.current = [];
    
    // Очищаем ВСЕ слои на карте
    clearAllMapLayers();
    
    setHasFinalPolygon(false);
    setLayouts([]);
    setSelectedLayout(null);
  }, [clearAllMapLayers]);

  const acceptPolygon = useCallback(() => {
    if (polygonPoints.length >= 3 && map.current) {
      drawFinalPolygon();
      setIsDrawingPolygon(false);
      setHasFinalPolygon(true);
      
      // Очищаем временную линию
      if (map.current.getLayer('temp-line')) {
        map.current.removeLayer('temp-line');
      }
      if (map.current.getSource('temp-line')) {
        map.current.removeSource('temp-line');
      }
      
      // Удаляем маркеры
      markers.current.forEach(marker => marker.remove());
      markers.current = [];
    }
  }, [polygonPoints, drawFinalPolygon]);

  const generateBuildings = async () => {
    if (!hasFinalPolygon) {
      alert('Сначала выберите участок!');
      return;
    }

    if (!map.current) {
      alert('Карта не готова');
      return;
    }

    setIsLoading(true);

    try {
      // Собираем координаты полигона в GeoJSON-формате (закрываем полигон)
      const coords = polygonPoints.map(p => [p.lng, p.lat]);
      if (coords.length >= 3) {
        const first = coords[0];
        const last = coords[coords.length - 1];
        if (first[0] !== last[0] || first[1] !== last[1]) {
          coords.push(first);
        }
      }

      // Формат запроса соответствующий шаблону: { PolygonPoints: [{lng,lat},...], MaxFloors, GrossFloorArea }
      const body = {
        PolygonPoints: polygonPoints.map(p => ({ lng: p.lng, lat: p.lat })),
        MaxFloors: null,
        GrossFloorArea: null
      };

      const res = await fetch('http://localhost:8080/api/Layouts/generatelayouts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(body)
      });

      if (!res.ok) {
        throw new Error(`Server returned ${res.status}`);
      }

      const data = await res.json();

      // Ожидаем либо массив концепций, либо объект с полем layouts
      const receivedLayouts = Array.isArray(data) ? data : (data.layouts || data.result || []);

      if (!receivedLayouts || receivedLayouts.length === 0) {
        // Падение на демо-данные если сервер вернул пусто
        setLayouts(demoLayouts);
        if (demoLayouts.length > 0) {
          setSelectedLayout(demoLayouts[0].name);
          renderBuildingsOnMap(demoLayouts[0].sections, demoLayouts[0].name);
        }
      } else {
        setLayouts(receivedLayouts);
        setSelectedLayout(receivedLayouts[0].name);
        renderBuildingsOnMap(receivedLayouts[0].sections, receivedLayouts[0].name);
      }

    } catch (err) {
      console.error('Ошибка при генерации проектов:', err);
      alert('Ошибка при обращении к серверу. Показаны демо-данные.');
      setLayouts(demoLayouts);
      if (demoLayouts.length > 0) {
        setSelectedLayout(demoLayouts[0].name);
        renderBuildingsOnMap(demoLayouts[0].sections, demoLayouts[0].name);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const selectLayout = (layoutName) => {
    setSelectedLayout(layoutName);
    const layout = layouts.find(l => l.name === layoutName);
    if (layout && layout.sections) {
      renderBuildingsOnMap(layout.sections, layout.name);
    }
  };

  return (
    <div className="map-app">
      <Sidebar
        isDrawingPolygon={isDrawingPolygon}
        polygonPoints={polygonPoints}
        hasFinalPolygon={hasFinalPolygon}
        getPointsText={getPointsText}
        onStartDrawing={startDrawingPolygon}
        onAcceptPolygon={acceptPolygon}
        onRemoveLastPoint={removeLastPoint}
        onClearPolygon={clearPolygon}
        onGenerateBuildings={generateBuildings}
        layouts={layouts}
        selectedLayout={selectedLayout}
        onSelectLayout={selectLayout}
        isLoading={isLoading}
      />
      
      <div className="map-panel">
        <div ref={mapContainer} className="map-container" />
        
        <div className="view-controls">
          <button 
            className={`view-toggle-btn ${is3DView ? 'active' : ''}`}
            onClick={toggle3DView}
            disabled={isLoading}
          >
            {isLoading ? (
              <>
                <span className="spinner"></span>
                Загрузка...
              </>
            ) : is3DView ? (
              <>
                <i className="fas fa-cube"></i>
                3D
              </>
            ) : (
              <>
                <i className="fas fa-map"></i>
                2D
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default MapApp;