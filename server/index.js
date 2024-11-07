const restify = require('restify');
const { MongoClient } = require('mongodb');

const server = restify.createServer();
server.use(restify.plugins.bodyParser());

// MongoDB connection URL
const mongoUrl = 'mongodb://localhost:27017';
const dbName = 'water_coolers_db';
let db;

// Connect to MongoDB
MongoClient.connect(mongoUrl)
    .then(client => {
        console.log('Connected to MongoDB');
        db = client.db(dbName);
    })
    .catch(err => {
        console.error('Failed to connect to MongoDB:', err);
    });

// GET all coolers
server.get('/water_coolers', function(req, res, next) {
    db.collection('readings')
        .distinct('coolerId')
        .then(coolers => {
            res.send({
                status: 'success',
                total: coolers.length,
                coolers: coolers.filter(id => id !== "")
            });
            return next();
        })
        .catch(err => {
            res.send(500, { error: err.message });
            return next();
        });
});

// GET specific cooler data
server.get('/water_coolers/:id', function(req, res, next) {
    db.collection('readings')
        .find({ coolerId: req.params['id'] })
        .sort({ timestamp: -1 })
        .limit(10)
        .toArray()
        .then(readings => {
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

            res.send({
                status: 'success',
                coolerId: req.params['id'],
                lastUpdate: readings[0]?.timestamp,
                stats: stats,
                readings: readings
            });
            return next();
        })
        .catch(err => {
            res.send(500, { error: err.message });
            return next();
        });
});

// POST new reading
server.post('/water_coolers/:id', function(req, res, next) {
    try {
        // Parse e valida i dati in arrivo
        let data = typeof req.body === 'string' ? JSON.parse(req.body) : req.body;
        
        const reading = {
            coolerId: req.params['id'],
            measurement: data.measurement,
            value: data.value,
            timestamp: new Date(data.timestamp)
        };

        console.log('Received reading:', reading);  // Debug log

        db.collection('readings')
            .insertOne(reading)
            .then(() => {
                res.send({
                    status: 'success',
                    message: 'Data saved successfully',
                    reading: reading
                });
                return next();
            })
            .catch(err => {
                console.error('Error saving to DB:', err);  // Debug log
                res.send(500, { error: err.message });
                return next();
            });
    } catch (err) {
        console.error('Error processing request:', err);  // Debug log
        res.send(400, { error: 'Invalid data format' });
        return next();
    }
});

server.listen(8011, function() {
    console.log('%s listening at %s', server.name, server.url);
});