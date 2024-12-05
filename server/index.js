const amqp = require('amqplib');
const { MongoClient } = require('mongodb');

// MongoDB connection URL
const mongoUrl = 'mongodb://localhost:27017';
const dbName = 'water_coolers_db';
let db;
let mongoClient;

// AMQP connection URL
const amqpUrl = 'amqp://127.0.0.1';
let amqpConnection;
let amqpChannel;

// Exchange settings
const exchangeName = 'water_coolers';
const exchangeType = 'topic';

async function connectToMongo() {
    try {
        mongoClient = await MongoClient.connect(mongoUrl);
        console.log('Connected to MongoDB');
        db = mongoClient.db(dbName);

        // Create indexes
        await db.collection('readings').createIndex({ coolerId: 1, timestamp: -1 });
    } catch (err) {
        console.error('Failed to connect to MongoDB:', err);
        throw err;
    }
}

async function connectToAmqp() {
    try {
        amqpConnection = await amqp.connect(amqpUrl);
        console.log('Connected to AMQP broker');
        amqpChannel = await amqpConnection.createChannel();

        // Declare exchange
        await amqpChannel.assertExchange(exchangeName, exchangeType, { durable: true });
        await setupConsumers();
    } catch (err) {
        console.error('Failed to connect to AMQP broker:', err);
        throw err;
    }
}

async function setupConsumers() {
    try {
        // Queue for readings
        const readingsQueue = await amqpChannel.assertQueue('readings_queue', {
            durable: true,
            autoDelete: false
        });
        await amqpChannel.bindQueue(readingsQueue.queue, exchangeName, 'water_coolers.*.readings');

        // Queue for list requests
        const listQueue = await amqpChannel.assertQueue('list_queue', {
            durable: true,
            autoDelete: false
        });
        await amqpChannel.bindQueue(listQueue.queue, exchangeName, 'water_coolers.list');

        // Queue for data requests
        const dataQueue = await amqpChannel.assertQueue('data_queue', {
            durable: true,
            autoDelete: false
        });
        await amqpChannel.bindQueue(dataQueue.queue, exchangeName, 'water_coolers.*.data');

        // Set up consumers
        console.log('Setting up consumers...');

        amqpChannel.consume(readingsQueue.queue, handleReadingsMessage, { noAck: true });
        amqpChannel.consume(listQueue.queue, handleListMessage, { noAck: true });
        amqpChannel.consume(dataQueue.queue, handleDataMessage, { noAck: true });

        console.log('All consumers are set up and listening');
    } catch (err) {
        console.error('Error setting up consumers:', err);
        throw err;
    }
}

async function handleReadingsMessage(msg) {
    if (!msg) return;

    try {
        const data = JSON.parse(msg.content.toString());
        const coolerId = data.coolerId;
        console.log(`[LOG] Received reading for ${coolerId}:`, data);

        const reading = {
            coolerId: coolerId,
            measurement: data.measurement,
            value: data.value,
            timestamp: new Date(data.timestamp)
        };

        await db.collection('readings').insertOne(reading);

        const response = {
            status: 'success',
            reading: reading
        };

        amqpChannel.publish(
            exchangeName,
            `water_coolers.${coolerId}.readings.response`,
            Buffer.from(JSON.stringify(response))
        );

        console.log(`[LOG] Saved and responded to reading from ${coolerId}`);
    } catch (err) {
        console.error('Error processing reading:', err);
    }
}

async function handleListMessage(msg) {
    if (!msg) return;

    try {
        console.log('[LOG] Received list request');
        const coolers = await db.collection('readings')
            .distinct('coolerId', { coolerId: { $ne: '' } });

        const response = {
            status: 'success',
            total: coolers.length,
            coolers: coolers
        };

        amqpChannel.publish(
            exchangeName,
            'water_coolers.list.response',
            Buffer.from(JSON.stringify(response))
        );

        console.log('[LOG] Sent coolers list response');
    } catch (err) {
        console.error('Error processing list request:', err);
    }
}

async function handleDataMessage(msg) {
    if (!msg) return;

    try {
        const routingKey = msg.fields.routingKey;
        const coolerId = routingKey.split('.')[1];
        console.log(`[LOG] Received data request for ${coolerId}`);

        const readings = await db.collection('readings')
            .find({ coolerId: coolerId })
            .sort({ timestamp: -1 })
            .limit(10)
            .toArray();

        const stats = {};
        readings.forEach(reading => {
            if (!stats[reading.measurement]) {
                stats[reading.measurement] = {
                    values: [],
                    lastValue: reading.value
                };
            }
            stats[reading.measurement].values.push(reading.value);
        });

        // Calculate statistics
        Object.keys(stats).forEach(measurement => {
            const values = stats[measurement].values;
            stats[measurement] = {
                average: values.reduce((a, b) => a + b, 0) / values.length,
                min: Math.min(...values),
                max: Math.max(...values),
                lastValue: stats[measurement].lastValue
            };
        });

        const response = {
            status: 'success',
            coolerId: coolerId,
            lastUpdate: readings[0]?.timestamp,
            stats: stats,
            readings: readings
        };

        amqpChannel.publish(
            exchangeName,
            `water_coolers.${coolerId}.data.response`,
            Buffer.from(JSON.stringify(response))
        );

        console.log(`[LOG] Sent data response for ${coolerId}`);
    } catch (err) {
        console.error('Error processing data request:', err);
    }
}

// Startup
async function start() {
    await connectToMongo();
    await connectToAmqp();
    console.log('Server is ready');
}

// Graceful shutdown
async function shutdown() {
    try {
        await mongoClient?.close();
        await amqpConnection?.close();
        console.log('Connections closed');
        process.exit(0);
    } catch (err) {
        console.error('Error during shutdown:', err);
        process.exit(1);
    }
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

start().catch(err => {
    console.error('Error starting the application:', err);
    process.exit(1);
});