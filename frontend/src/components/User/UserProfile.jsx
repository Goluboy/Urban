import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import './User.css';

const UserProfile = () => {
  const { user, updateUser } = useAuth();
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState({
    name: user?.name || '',
    email: user?.email || '',
    company: user?.company || '',
    phone: user?.phone || ''
  });
  const [message, setMessage] = useState('');

  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  const handleSave = () => {
    updateUser(formData);
    setMessage('Профиль успешно обновлен!');
    setIsEditing(false);
    
    setTimeout(() => setMessage(''), 3000);
  };

  if (!user) {
    return <div className="not-authorized">Пожалуйста, войдите в систему</div>;
  }

  return (
    <div className="profile-container">
      <div className="profile-header">
        <h2>Профиль пользователя</h2>
        {!isEditing && (
          <button 
            className="btn btn-outline"
            onClick={() => setIsEditing(true)}
          >
            <i className="fas fa-edit"></i> Редактировать
          </button>
        )}
      </div>

      {message && <div className="alert alert-success">{message}</div>}

      <div className="profile-card">
        <div className="profile-avatar">
          <div className="avatar-circle">
            {user.name.charAt(0).toUpperCase()}
          </div>
          <h3>{user.name}</h3>
          <p className="user-role">{user.role === 'admin' ? 'Администратор' : 'Пользователь'}</p>
        </div>

        <div className="profile-info">
          {isEditing ? (
            <div className="edit-form">
              <div className="form-group">
                <label>Имя</label>
                <input
                  type="text"
                  name="name"
                  value={formData.name}
                  onChange={handleChange}
                  placeholder="Введите ваше имя"
                />
              </div>
              
              <div className="form-group">
                <label>Email</label>
                <input
                  type="email"
                  name="email"
                  value={formData.email}
                  onChange={handleChange}
                  placeholder="Введите ваш email"
                />
              </div>
              
              <div className="form-group">
                <label>Компания</label>
                <input
                  type="text"
                  name="company"
                  value={formData.company}
                  onChange={handleChange}
                  placeholder="Название компании"
                />
              </div>
              
              <div className="form-group">
                <label>Телефон</label>
                <input
                  type="tel"
                  name="phone"
                  value={formData.phone}
                  onChange={handleChange}
                  placeholder="Ваш телефон"
                />
              </div>
              
              <div className="form-actions">
                <button className="btn btn-primary" onClick={handleSave}>
                  Сохранить
                </button>
                <button 
                  className="btn btn-outline" 
                  onClick={() => {
                    setIsEditing(false);
                    setFormData({
                      name: user.name,
                      email: user.email,
                      company: user.company || '',
                      phone: user.phone || ''
                    });
                  }}
                >
                  Отмена
                </button>
              </div>
            </div>
          ) : (
            <div className="info-grid">
              <div className="info-item">
                <span className="info-label">Имя:</span>
                <span className="info-value">{user.name}</span>
              </div>
              <div className="info-item">
                <span className="info-label">Email:</span>
                <span className="info-value">{user.email}</span>
              </div>
              <div className="info-item">
                <span className="info-label">Компания:</span>
                <span className="info-value">{user.company || 'Не указана'}</span>
              </div>
              <div className="info-item">
                <span className="info-label">Телефон:</span>
                <span className="info-value">{user.phone || 'Не указан'}</span>
              </div>
              <div className="info-item">
                <span className="info-label">Дата регистрации:</span>
                <span className="info-value">
                  {user.createdAt ? new Date(user.createdAt).toLocaleDateString() : 'Неизвестно'}
                </span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Убрана секция со статистикой */}
    </div>
  );
};

export default UserProfile;