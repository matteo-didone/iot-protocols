const mqtt = require('mqtt');
const { MongoClient } = require('mongodb');

// MongoDB connection URL
const mongoUrl = 'mongodb://localhost:27017';
const dbName = 'water_coolers_db';
let db;
let mongoClient;

// MQTT broker URL
const brokerUrl = 'mqtt://localhost:1883';
const client = mqtt.connect(brokerUrl);

// Connect to MongoDB
async function connectToMongo() {
    try {
        mongoClient = await MongoClient.connect(mongoUrl);
        console.log('Connected to MongoDB');
        db = mongoClient.db(dbName);
    } catch (err) {
        console.error('Failed to connect to MongoDB:', err);
    }
}

// Connect to MongoDB before starting MQTT
connectToMongo().then(() => {
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
                console.error('Error fetching cooler list:', err);
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
                console.error(`Error fetching data for cooler ${coolerId}:`, err);
                client.publish(`water_coolers/${coolerId}/data/response`, JSON.stringify({
                    status: 'error',
                    error: err.message
                }));
            }
        }
        else if (topic.match(/water_coolers\/(.+)\/readings/)) {
            // POST new reading
            try {
                const data = JSON.parse(message.toString());
                const coolerId = data.coolerId; // Prendi il coolerId dal payload invece che dal topic

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
                // Usa il coolerId dal payload se disponibile
                const errorResponse = {
                    status: 'error',
                    error: err.message
                };
                try {
                    const data = JSON.parse(message.toString());
                    if (data.coolerId) {
                        client.publish(`water_coolers/${data.coolerId}/readings/response`, JSON.stringify(errorResponse));
                    }
                } catch (parseErr) {
                    console.error('Error parsing message in error handler:', parseErr);
                }
            }
        }
    });

    // Error handling
    client.on('error', (err) => {
        console.error('MQTT error:', err);
    });

    console.log('MQTT server is running...');
});

// Graceful shutdown
process.on('SIGINT', async () => {
    try {
        await mongoClient?.close();
        client.end();
        console.log('Connections closed');
        process.exit(0);
    } catch (err) {
        console.error('Error during shutdown:', err);
        process.exit(1);
    }
});