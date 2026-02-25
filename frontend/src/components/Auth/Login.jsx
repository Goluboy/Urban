import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import './Auth.css';

const Login = ({ onSwitchToRegister }) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      // В реальном приложении здесь будет запрос к API
      // Для демо используем фиктивных пользователей
      const mockUsers = [
        { id: 1, email: 'user@example.com', password: 'password123', name: 'Тестовый пользователь' },
        { id: 2, email: 'admin@example.com', password: 'admin123', name: 'Администратор', role: 'admin' }
      ];

      const user = mockUsers.find(u => u.email === email && u.password === password);
      
      if (user) {
        const { password: _, ...userData } = user; // Убираем пароль из объекта
        login(userData);
      } else {
        setError('Неверный email или пароль');
      }
    } catch (err) {
      setError('Ошибка при входе. Попробуйте еще раз.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h2>Вход в систему</h2>
        
        {error && <div className="alert alert-error">{error}</div>}
        
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email</label>
            <input
              type="email"
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Введите ваш email"
              required
              disabled={loading}
            />
          </div>
          
          <div className="form-group">
            <label htmlFor="password">Пароль</label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Введите пароль"
              required
              disabled={loading}
            />
          </div>
          
          <button 
            type="submit" 
            className="btn btn-primary btn-block"
            disabled={loading}
          >
            {loading ? 'Вход...' : 'Войти'}
          </button>
        </form>
        
        <div className="auth-footer">
          <p>Нет аккаунта? <button onClick={onSwitchToRegister} className="btn-link">Зарегистрироваться</button></p>
        </div>
        
        <div className="demo-credentials">
          <h4>Демо доступ:</h4>
          <p><strong>Email:</strong> user@example.com</p>
          <p><strong>Пароль:</strong> password123</p>
        </div>
      </div>
    </div>
  );
};

export default Login;
