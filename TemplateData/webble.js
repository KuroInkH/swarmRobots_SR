// device
var deviceCount = 0;
var deviceTable = {};
var deviceEventTable = {};
var deviceIdTable = {};
// server
var serverCount = 0;
var serverTable = {};
var serverIdTable = {};
// service
var serviceCount = 0;
var serviceTable = {};
var serviceIdTable = {};
// characteristic
var characteristicCount = 0;
var characteristicTable = {};
var characteristicIdTable = {};
var characteristicNotificationTable = {};

// callback(int deviceID, string deviceUUID, string deviceName)
function bluetooth_requestDevice(SERVICE_UUID, callback, errorCallback)
{
    let options = {};
    options.filters = [ {services: [SERVICE_UUID]}, ];
    navigator.bluetooth.requestDevice(options)
    .then(device => {
        let id = deviceCount;
        if (device.id in deviceIdTable)
        {
            id = deviceIdTable[device.id];
        }
        else
        {
            deviceIdTable[device.id] = id;
            deviceCount++;
        }
        deviceTable[id] = device;
        callback(id, device.id, device.name);
    })
    .catch(error => {
        errorCallback(error.message);
    });
}

// callback(int deviceID, int serverID, int serviceID, string serviceUUID)
// disconnectCallback(int deviceID)
function server_connect(deviceID, SERVICE_UUID, callback, disconnectCallback)
{
    deviceTable[deviceID].gatt.connect()
    .then(server => {
        let ondisconnected = () => {
            deviceTable[deviceID].removeEventListener('gattserverdisconnected', deviceEventTable[deviceID]);
            delete deviceEventTable[deviceID];
            disconnectCallback(deviceID);
        }
        deviceEventTable[deviceID] = ondisconnected;
        deviceTable[deviceID].addEventListener('gattserverdisconnected', ondisconnected);
        let id = serverCount;
        if (server.device.id in serverIdTable)
        {
            id = serverIdTable[server.device.id];
        }
        else
        {
            serverIdTable[server.device.id] = id;
            serverCount++;
        }
        serverTable[id] = server;
        server_getPrimaryService(id, SERVICE_UUID, (serviceID, serviceUUID) => { callback(deviceID, id, serviceID, serviceUUID); });
    });
}

// callback(int serviceID, string serviceUUID)
function server_getPrimaryService(serverID, SERVICE_UUID, callback)
{
    serverTable[serverID].getPrimaryService(SERVICE_UUID)
    .then(service => {
        let id = serviceCount;
        let uuid = create_serviceUUID(serverID, service.uuid);
        if (uuid in serviceIdTable)
        {
            id = serviceIdTable[uuid];
        }
        else
        {
            serviceIdTable[uuid] = id;
            serviceCount++;
        }
        serviceTable[id] = service;
        callback(id, service.uuid);
    });
}

function server_disconnect(serverID)
{
    serverTable[serverID].disconnect();
}

function service_getCharacteristic(serviceID, characteristicUUID, callback)
{
    serviceTable[serviceID].getCharacteristic(characteristicUUID)
    .then(chara => {
        let id = characteristicCount;
        let uuid = create_characteristicUUID(serviceID, chara.uuid)
        if (uuid in characteristicIdTable)
        {
            id = characteristicIdTable[uuid];
        }
        else
        {
            characteristicIdTable[uuid] = id;
            characteristicCount++;
        }
        characteristicTable[id] = chara;
        callback(serviceID, id, characteristicUUID);
    });
}

function service_getCharacteristics(serviceID, callback)
{
    serviceTable[serviceID].getCharacteristics()
    .then(charas => {

        for(let i = 0; i < charas.length; i++)
        {
            let id = characteristicCount;
            let uuid = create_characteristicUUID(serviceID, charas[i].uuid)
            if (uuid in characteristicIdTable)
            {
                id = characteristicIdTable[uuid];
            }
            else
            {
                characteristicIdTable[uuid] = id;
                characteristicCount++;
            }

            characteristicTable[id] = charas[i];
            callback(serviceID, charas.length, i, id, charas[i].uuid);
        }
    });
}

// Writes go through a queue per robot: Chrome rejects a write while the previous one to the same
// robot is still in progress ("GATT operation already in progress"), and before this queue such
// commands were silently lost. On slower Bluetooth links (often Windows) the toio SDK sends motor
// commands faster than they can go out, so only the newest pending command per characteristic
// is kept: a robot never acts on a stale command and the backlog never grows.
// Every 10 s the console shows how the link is coping ("[BLE] ...").
var bleWriteQueues = {};
var bleWriteStats = { sent: 0, replaced: 0, failed: 0, timeouts: 0, totalMs: 0, maxMs: 0, lastError: "" };
var BLE_WRITE_TIMEOUT_MS = 1000;

function characteristic_writeValue(characteristicID, bytes)
{
    let chara = characteristicTable[characteristicID];
    if (!chara) return;
    let key = chara.service.device.id;
    let q = bleWriteQueues[key];
    if (!q) { q = bleWriteQueues[key] = { busy: false, order: [], pending: {} }; }
    if (characteristicID in q.pending) { bleWriteStats.replaced++; }
    else { q.order.push(characteristicID); }
    // Copy: bytes is a view into Unity's memory, which is reused after this call returns
    q.pending[characteristicID] = bytes.slice();
    ble_pumpWrites(q);
}

function ble_pumpWrites(q)
{
    if (q.busy || q.order.length === 0) return;
    let id = q.order.shift();
    let bytes = q.pending[id];
    delete q.pending[id];
    let chara = characteristicTable[id];
    if (!chara) { ble_pumpWrites(q); return; }

    q.busy = true;
    let finished = false;
    let started = performance.now();
    let finish = () => {
        if (finished) return;
        finished = true;
        q.busy = false;
        ble_pumpWrites(q);
    };
    // Never let one stuck write block this robot forever
    setTimeout(() => { if (!finished) { bleWriteStats.timeouts++; finish(); } }, BLE_WRITE_TIMEOUT_MS);

    let write;
    try {
        // Motors (toio's motor characteristic allows it) go without response, as the SDK intends
        if (chara.properties && chara.properties.writeWithoutResponse && chara.writeValueWithoutResponse) {
            write = chara.writeValueWithoutResponse(bytes);
        } else if (chara.writeValueWithResponse) {
            write = chara.writeValueWithResponse(bytes);
        } else {
            write = chara.writeValue(bytes);
        }
    } catch (e) {
        write = Promise.reject(e);
    }
    write.then(() => {
        let ms = performance.now() - started;
        bleWriteStats.sent++;
        bleWriteStats.totalMs += ms;
        if (ms > bleWriteStats.maxMs) bleWriteStats.maxMs = ms;
    }).catch(e => {
        bleWriteStats.failed++;
        bleWriteStats.lastError = (e && e.message) ? e.message : String(e);
    }).finally(finish);
}

setInterval(() => {
    let s = bleWriteStats;
    if (s.sent + s.failed + s.timeouts + s.replaced === 0) return;
    let avg = s.sent ? (s.totalMs / s.sent).toFixed(0) : "-";
    console.log("[BLE] last 10 s: " + s.sent + " commands sent (avg " + avg + " ms, max " + s.maxMs.toFixed(0) + " ms), "
        + s.replaced + " replaced by a newer one, " + s.failed + " failed, " + s.timeouts + " timed out"
        + (s.lastError ? ". Last error: " + s.lastError : ""));
    bleWriteStats = { sent: 0, replaced: 0, failed: 0, timeouts: 0, totalMs: 0, maxMs: 0, lastError: "" };
}, 10000);

function characteristic_readValue(characteristicID, callback)
{
    characteristicTable[characteristicID].readValue()
    .then(response => {
        callback(characteristicID, response.buffer);
    });
}

// callback(int characteristicID, byte[] data)
function characteristic_startNotifications(characteristicID, callback)
{
    characteristicTable[characteristicID].startNotifications().then(char => {
        console.log('notifications started');
        let onchanged = (event) => {
            callback(characteristicID, event.target.value.buffer);
        };
        characteristicNotificationTable[characteristicID] = onchanged;
        char.addEventListener('characteristicvaluechanged', onchanged);
    });
}

function characteristic_stopNotifications(characteristicID)
{
    characteristicTable[characteristicID].stopNotifications().then(char => {
        char.removeEventListener('characteristicvaluechanged', characteristicNotificationTable[characteristicID]);
        delete characteristicNotificationTable[characteristicID];
    });
}

function ab2str(buf)
{
    return String.fromCharCode.apply(null, new Uint8Array(buf));
}

function create_serviceUUID(serverID, serviceUUID)
{
    return serverID + serviceUUID;
}

function create_characteristicUUID(serviceID, characteristicUUID)
{
    return serviceID + characteristicUUID;
}
