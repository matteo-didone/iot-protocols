const mqtt = require('mqtt');
const { MongoClient } = require('mongodb');

// MongoDB connection URL
const mongoUrl = 'mongodb://localhost:27017';
const dbName = 'water_coolers_db';
let db;

// MQTT broker URL
const brokerUrl = 'mqtt://localhost:1883';
const client = mqtt.connect(brokerUrl);

// Connect to MongoDB
MongoClient.connect(mongoUrl)
    .then(client => {
        console.log('Connected to MongoDB');
        db = client.db(dbName);
    })
    .catch(err => {
        console.error('Failed to connect to MongoDB:', err);
    });

// MQTT connection handler
client.on('connect', () => {
    console.log('Connected to MQTT broker');

    // Subscribe to topics
    client.subscribe('water_coolers/+/readings', (err) => {
        if (err) console.error('Subscription error:', err);
    });

    client.subscribe('water_coolers/list', (err) => {
        if (err) console.error('Subscription error:', err);
    });

    client.subscribe('water_coolers/+/data', (err) => {
        if (err) console.error('Subscription error:', err);
    });
});

// Handle incoming messages
client.on('message', async (topic, message) => {
    console.log(`Received message on ${topic}`);

    // Handle different topics
    if (topic === 'water_coolers/list') {
        // GET all coolers
        try {
            const coolers = await db.collection('readings').distinct('coolerId');
            client.publish('water_coolers/list/response', JSON.stringify({
                status: 'success',
                total: coolers.length,
                coolers: coolers.filter(id => id !== "")
            }));
        } catch (err) {
            client.publish('water_coolers/list/response', JSON.stringify({
                status: 'error',
                error: err.message
            }));
        }
    }
    else if (topic.match(/water_coolers\/(.+)\/data/)) {
        // GET specific cooler data
        const coolerId = topic.split('/')[1];
        try {
            const readings = await db.collection('readings')
                .find({ coolerId: coolerId })
                .sort({ timestamp: -1 })
                .limit(10)
                .toArray();

            // Raggruppa le letture per tipo di misurazione
            const measurementGroups = {};
            readings.forEach(reading => {
                const type = reading.measurement;
                if (!measurementGroups[type]) {
                    measurementGroups[type] = [];
                }
                measurementGroups[type].push(reading.value);
            });

            // Calcola le statistiche per ogni tipo di misurazione
            const stats = {};
            Object.keys(measurementGroups).forEach(type => {
                const values = measurementGroups[type];
                stats[type] = {
                    average: values.reduce((a, b) => a + b, 0) / values.length,
                    min: Math.min(...values),
                    max: Math.max(...values),
                    lastValue: values[0]
                };
            });

            client.publish(`water_coolers/${coolerId}/data/response`, JSON.stringify({
                status: 'success',
                coolerId: coolerId,
                lastUpdate: readings[0]?.timestamp,
                stats: stats,
                readings: readings
            }));
        } catch (err) {
            client.publish(`water_coolers/${coolerId}/data/response`, JSON.stringify({
                status: 'error',
                error: err.message
            }));
        }
    }
    else if (topic.match(/water_coolers\/(.+)\/readings/)) {
        // POST new reading
        try {
            const coolerId = topic.split('/')[1];
            const data = JSON.parse(message.toString());

            const reading = {
                coolerId: coolerId,
                measurement: data.measurement,
                value: data.value,
                timestamp: new Date(data.timestamp)
            };

            console.log('Received reading:', reading);

            await db.collection('readings').insertOne(reading);

            client.publish(`water_coolers/${coolerId}/readings/response`, JSON.stringify({
                status: 'success',
                message: 'Data saved successfully',
                reading: reading
            }));
        } catch (err) {
            console.error('Error processing message:', err);
            client.publish(`water_coolers/${coolerId}/readings/response`, JSON.stringify({
                status: 'error',
                error: err.message
            }));
        }
    }
});

// Error handling
client.on('error', (err) => {
    console.error('MQTT error:', err);
});

console.log('MQTT server is running...');