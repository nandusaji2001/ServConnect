// MongoDB initialization script for local development
db = db.getSiblingDB('ServConnectDb');

// Create a user for the application
db.createUser({
  user: 'servconnect',
  pwd: 'servconnect123',
  roles: [
    {
      role: 'readWrite',
      db: 'ServConnectDb'
    }
  ]
});

// Create initial collections (optional)
db.createCollection('Users');
db.createCollection('Services');
db.createCollection('Bookings');
db.createCollection('Advertisements');

print('Database initialized successfully!');
