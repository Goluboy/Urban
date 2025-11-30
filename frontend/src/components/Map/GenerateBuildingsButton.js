import { useState } from 'react';
import './GenerateBuildingsButton.css';

function GenerateBuildingsButton() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  const handleGenerateBuildings = async () => {
    setLoading(true);
    setError(null);
    setSuccess(false);

    try {
      const response = await fetch('https://localhost:7233/api/Urban/GenerateBuildings', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({"PolygonPoints":[{"lng":60.62371514638414,"lat":56.83843014892511},{"lng":60.62384783664828,"lat":56.83796078354237},{"lng":60.62457321009347,"lat":56.838139820516744},{"lng":60.62440513575817,"lat":56.83843982650015}],"MaxFloors":null,"GrossFloorArea":null}),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      setSuccess(true);
      console.log('Buildings generated successfully');
    } catch (err) {
      setError(err.message);
      console.error('Error:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="generate-buildings-btn">
      <button 
        onClick={handleGenerateBuildings} 
        disabled={loading}
        className="btn-generate"
      >
        {loading ? 'Генерация...' : 'Генерировать здания'}
      </button>
      {success && <p className="success-message">✓ Здания успешно сгенерированы</p>}
      {error && <p className="error-message">✗ Ошибка: {error}</p>}
    </div>
  );
}

export default GenerateBuildingsButton;