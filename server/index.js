// Importa il framework Restify per creare e gestire il server REST
var restify = require('restify');

// Crea una nuova istanza del server Restify
var server = restify.createServer();

// Aggiunge il middleware bodyParser che permette di processare il corpo delle richieste HTTP
// Utile per gestire dati JSON nelle richieste POST
server.use(restify.plugins.bodyParser());

// Definisce un endpoint GET che restituisce la lista di tutti i water coolers
// Quando si fa una richiesta GET a '/water_coolers'
server.get('/water_coolers', function(req, res, next) {
    res.send('List of coolers: [TODO]');  // Risponde con un messaggio (da implementare)
    return next();  // Passa al prossimo handler nella catena di middleware
});

// Definisce un endpoint GET per ottenere informazioni su un singolo water cooler
// :id è un parametro dinamico nell'URL (es: /water_coolers/123)
server.get('/water_coolers/:id', function(req, res, next) {
    res.send('Current values for cooler ' + req.params['id'] + ': [TODO]');  // Risponde con i dati del cooler specifico
    return next();
});

// Definisce un endpoint POST per ricevere dati da un water cooler specifico
// Usato quando un cooler invia nuovi dati al server
server.post('/water_coolers/:id', function(req, res, next) {
    res.send('Data received from cooler [TODO]');  // Conferma la ricezione dei dati
    console.log(req.body);  // Stampa i dati ricevuti nella console del server
    return next();
});

// Avvia il server sulla porta 8011
// Il punto di partenza per avere la parte JS è questo
server.listen(8011, function() {
    console.log('%s listening at %s', server.name, server.url);  // Stampa un messaggio di conferma quando il server è attivo
});