# IoT Protocols: Esempi di Comunicazione MQTT

Questo repository contiene esercizi ed esempi relativi alla comunicazione tra dispositivi IoT, con particolare focus su MQTT (Message Queuing Telemetry Transport), sviluppati per il modulo **"Protocolli IOT"** del corso Digital Solutions 2023-2025.

## Struttura del Repository

- **`client/NetCoreClient`**  
  Contiene un'applicazione sviluppata in C# con .NET Core che simula un dispositivo IoT. Questo client invia dati periodici al broker MQTT e riceve comandi specifici per eseguire azioni.

- **`server`**  
  Include codice e configurazioni per la gestione dei dati ricevuti dal client e la loro memorizzazione in un database.

- **`README.md`**  
  Questo file fornisce una panoramica dettagliata del progetto e istruzioni per configurare e utilizzare il codice.

## Funzionalità Principali

### Client IoT (NetCoreClient)

1. **Invio Dati al Broker MQTT**  
   Il client invia dati periodicamente al broker MQTT sul topic `water_coolers/cooler_001/readings`.  
   I dati includono: 
   - `coolerId`: l'ID del dispositivo
   - `measurement`: tipo di misurazione (es. "water_flow")
   - `value`: valore della misurazione
   - `timestamp`: orario del rilevamento

2. **Ricezione di Comandi**  
   Il client ascolta comandi sui topic `commands/cooler_001/#` e li elabora.  
   Comandi supportati:  
   - **`power`**: accensione/spegnimento del dispositivo  
   - **`night_light`**: accensione/spegnimento della luce notturna  
   - **`maintenance`**: abilitazione/disabilitazione della modalità manutenzione  

3. **Validazione dei Comandi**  
   Ogni comando ricevuto viene validato. I messaggi devono essere in formato JSON con campi corretti:
   - `action`: tipo di comando (`power`, `night_light`, `maintenance`)
   - `state` o `enabled`: stato del comando (booleano)

### Server

Il server è progettato per ricevere i dati dal client, memorizzarli in un database e fornire un'interfaccia per l'analisi.

## Configurazione

### Prerequisiti

- **Broker MQTT**: Il progetto utilizza Mosquitto come broker MQTT. Installalo seguendo le istruzioni del [sito ufficiale](https://mosquitto.org/).
- **.NET Core SDK**: Per eseguire il client, installa il .NET Core SDK. Puoi scaricarlo da [Microsoft](https://dotnet.microsoft.com/).

### Setup del Broker MQTT

1. Assicurati che Mosquitto sia installato e in esecuzione sulla porta predefinita `1883`:
   ```bash
   mosquitto -v
