# Water Cooler Monitoring System with RabbitMQ

## Architettura del Sistema

Il sistema utilizza RabbitMQ come message broker, implementando un pattern di comunicazione basato su exchange e code. L'architettura è stata progettata per gestire efficacemente il monitoraggio di multiple casette dell'acqua.

### Exchange e Routing

- **Exchange Principale**: `water_coolers`
  - Tipo: Topic
  - Durabile: Sì
  - Gestisce tutti i messaggi del sistema

### Pattern di Comunicazione

```
[Casette] → [Exchange] → [Binding Rules] → [Code] → [Consumers]
```

### Struttura delle Code

Per ogni casetta vengono create code specifiche:
- `readings.<cooler_id>`: per le letture dei flussi d'acqua
- `commands.<cooler_id>`: per i comandi di controllo
- `data.<cooler_id>`: per le statistiche e i dati aggregati

### Routing Keys

Il sistema utilizza routing keys strutturate:
- `water_coolers.<cooler_id>.readings`: per le letture
- `water_coolers.<cooler_id>.data`: per i dati statistici
- `water_coolers.list`: per la lista delle casette
- `water_coolers.<cooler_id>.commands`: per i comandi

### Binding Rules

Le regole di binding permettono di:
- Instradare i messaggi alla coda corretta in base al cooler_id
- Separare i flussi di dati dai comandi
- Gestire le risposte in modo indipendente

### Flusso dei Dati

1. **Pubblicazione**:
   - Le casette pubblicano i dati sull'exchange `water_coolers`
   - Ogni messaggio include il cooler_id nel routing key

2. **Routing**:
   - L'exchange instrada i messaggi alle code appropriate
   - Il routing è basato sui pattern delle routing keys

3. **Consumo**:
   - Consumer dedicati processano i messaggi dalle code
   - Ogni consumer gestisce uno specifico tipo di messaggio

### Implementazione

#### Client (.NET)
```csharp
// Invio letture
protocol.SendCommand($"water_coolers.{coolerId}.readings", data);

// Richiesta dati
protocol.SendCommand($"water_coolers.{coolerId}.data", "{}");

// Richiesta lista casette
protocol.SendCommand("water_coolers.list", "{}");
```

#### Server (Node.js)
```javascript
// Binding per le letture
channel.bindQueue(readingsQueue, exchangeName, 'water_coolers.*.readings');

// Binding per i comandi
channel.bindQueue(commandsQueue, exchangeName, 'water_coolers.*.commands');

// Binding per i dati
channel.bindQueue(dataQueue, exchangeName, 'water_coolers.*.data');
```

### Vantaggi dell'Architettura

1. **Scalabilità**:
   - Facile aggiunta di nuove casette
   - Possibilità di scalare i consumer indipendentemente

2. **Separazione delle Responsabilità**:
   - Flussi di dati separati dai comandi
   - Code dedicate per ogni tipo di messaggio

3. **Affidabilità**:
   - Code durabili per la persistenza dei messaggi
   - Gestione automatica del reconnect

4. **Flessibilità**:
   - Routing configurabile tramite binding rules
   - Facile aggiunta di nuovi tipi di messaggi

### Conclusioni

Questa implementazione fornisce una base solida per il monitoraggio delle casette dell'acqua, con una chiara separazione tra dati e comandi, e la possibilità di scalare facilmente il sistema aggiungendo nuove casette o funzionalità.
