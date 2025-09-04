let _db = null

const toPromise = (req) =>
    new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result)
        req.onerror = () => reject(req.error)
    })

export function openDb(dbName, storeNames, version = 1) {
    if (_db)
        return Promise.resolve(_db)

    return new Promise((resolve, reject) => {
        const req = indexedDB.open(dbName, version)

        req.onupgradeneeded = (ev) => {
            const db = ev.target.result
            
            for (const name of storeNames) {
                if (!db.objectStoreNames.contains(name)) {
                    db.createObjectStore(name)
                }
            }
        }

        req.onsuccess = () => {
            _db = req.result

            // Keep handle fresh / auto-close on upgrades
            _db.onversionchange = () => {
                try {
                    _db.close()
                } finally {
                    _db = null
                }
            }
            _db.onclose = () => { _db = null; }

            resolve(_db)
        }

        req.onerror = () => reject(req.error)
    })
}

export function withStore(storeName, mode, work) {
    if (!_db)
        return Promise.reject(new Error("DB not opened. Call init first."))

    const tx = _db.transaction(storeName, mode)
    const store = tx.objectStore(storeName)

    // Allow work to return a value or a promise.
    const resultP = Promise.resolve(work(store, tx))

    return new Promise((resolve, reject) => {
        // Complete → settle with work's result (success or failure)
        tx.oncomplete = () => resultP.then(resolve, reject)

        // Abort → reject with the transaction error
        tx.onabort = () => reject(tx.error || new DOMException("AbortError", "AbortError"))

        // Avoid uncaught event errors bubbling to window
        tx.onerror = (ev) => {
            ev.preventDefault?.()
            ev.stopPropagation?.()
        }
    })
}

// noinspection JSUnusedGlobalSymbols
export async function init(dbName, storeNames, version) {
    await openDb(dbName, storeNames, version || 1)
    return true
}

// noinspection JSUnusedGlobalSymbols
export async function set(storeName, key, value) {
    return withStore(storeName, "readwrite", (store) => {
        const req = store.put(value, key)
        return toPromise(req).then(() => true)
    })
}

// noinspection JSUnusedGlobalSymbols
export async function get(storeName, key) {
    return withStore(storeName, "readonly", (store) => {
        const req = store.get(key)
        return toPromise(req).then((v) => (v ?? null))
    })
}

// noinspection JSUnusedGlobalSymbols
export async function getAll(storeName) {
    return withStore(storeName, "readonly", (store) => {
        // Fast path when supported
        if (store.getAll && store.getAllKeys) {
            return Promise.all([toPromise(store.getAllKeys()), toPromise(store.getAll())])
                .then(([keys = [], values = []]) => values.map((v, i) => [keys[i], v]))
        }

        // Portable cursor path (Safari < 14, etc.)
        return new Promise((resolve, reject) => {
            const items = []
            const req = store.openCursor()
            
            req.onsuccess = (ev) => {
                const cursor = ev.target.result
                
                if (cursor) {
                    items.push([cursor.key, cursor.value])
                    cursor.continue()
                } else {
                    resolve(items)
                }
            }
            
            req.onerror = () => reject(req.error)
        })
    })
}

// noinspection JSUnusedGlobalSymbols
export async function del(storeName, key) {
    return withStore(storeName, "readwrite", (store) => {
        const req = store.delete(key)
        return toPromise(req).then(() => true)
    })
}

// noinspection JSUnusedGlobalSymbols
export async function keys(storeName) {
    return withStore(storeName, "readonly", (store) => {
        if (store.getAllKeys) {
            return toPromise(store.getAllKeys()).then((ks) => ks || [])
        }
        
        // Fallback
        return new Promise((resolve, reject) => {
            const ks = []
            const req = store.openKeyCursor()
            req.onsuccess = (ev) => {
                const cursor = ev.target.result
                if (cursor) {
                    ks.push(cursor.key)
                    cursor.continue()
                } else {
                    resolve(ks)
                }
            }
            req.onerror = () => reject(req.error)
        })
    })
}

// noinspection JSUnusedGlobalSymbols
export async function clear(storeName) {
    return withStore(storeName, "readwrite", (store) => {
        const req = store.clear()
        return toPromise(req).then(() => true)
    })
}
