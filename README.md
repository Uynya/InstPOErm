1. добавлена JWT-авторизация
2. добавлены endpoints /api/auth/register и /api/auth/login
3. реализована генерация и валидация JWT токенов
4. защищены все маршруты для работы с задачами
5. добавлено хэширование паролей 
6. добавлена привязка задач к пользователям
   
проверка:

<img width="1105" height="769" alt="image" src="https://github.com/user-attachments/assets/83924bae-a731-4b9c-b8dc-f739f2ca2447" />
<img width="1077" height="731" alt="image" src="https://github.com/user-attachments/assets/6da54144-357e-4d7d-8b7b-7806cfe9833b" />
<img width="855" height="698" alt="image" src="https://github.com/user-attachments/assets/c0c7c776-787f-4d2c-90f4-becd73359e4b" />
<img width="1076" height="781" alt="image" src="https://github.com/user-attachments/assets/22c97ad3-5d18-45d1-bbd9-2d4c6694646f" />
<img width="1109" height="920" alt="image" src="https://github.com/user-attachments/assets/d1d0b605-48ef-4a49-98b8-b038d8500f13" />

без токена или с неверным токеном выдаёт ошибку
