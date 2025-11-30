import React, { useEffect, useRef } from 'react';
import mapboxgl from 'mapbox-gl';
import 'mapbox-gl/dist/mapbox-gl.css';

const MapboxExample = () => {
  const mapContainerRef = useRef(null);
  const mapRef = useRef(null);

  useEffect(() => {
    if (!mapContainerRef.current) return;

    mapboxgl.accessToken = 'pk.eyJ1IjoiYWxleGtvZiIsImEiOiJjbWJ1Y2N3eGMwYmx4MmlzM3UzZzZjcDhqIn0.u9-tNoGFKoTucJ2hRPrtGQ';

    mapRef.current = new mapboxgl.Map({
      container: mapContainerRef.current, // Исправлено!
      style: 'mapbox://styles/mapbox/streets-v11',
      center: [60.5975, 56.8389],
      zoom: 13,
      pitch: 45,
      bearing: -17.6,
      antialias: true
    });

    mapRef.current.on('style.load', () => {
      const layers = mapRef.current.getStyle().layers;
      const labelLayerId = layers.find(
        (layer) => layer.type === 'symbol' && layer.layout['text-field']
      )?.id; // Добавлена безопасная проверка

      if (labelLayerId) {
        mapRef.current.addLayer(
          {
            id: 'add-3d-buildings',
            source: 'composite',
            'source-layer': 'building',
            filter: ['==', 'extrude', 'true'],
            type: 'fill-extrusion',
            minzoom: 15,
            paint: {
              'fill-extrusion-color': '#aaa',
              'fill-extrusion-height': [
                'interpolate',
                ['linear'],
                ['zoom'],
                15,
                0,
                15.05,
                ['get', 'height']
              ],
              'fill-extrusion-base': [
                'interpolate',
                ['linear'],
                ['zoom'],
                15,
                0,
                15.05,
                ['get', 'min_height']
              ],
              'fill-extrusion-opacity': 0.6
            }
          },
          labelLayerId
        );
      }
    });

    return () => {
      if (mapRef.current) {
        mapRef.current.remove();
        mapRef.current = null;
      }
    };
  }, []);

  return (
    <div 
      ref={mapContainerRef} 
      style={{ 
        height: '400px', // Лучше явно указать высоту
        width: '100%' 
      }} 
    />
  );
};

export default MapboxExample;