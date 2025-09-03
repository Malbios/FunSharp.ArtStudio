let _db = null

function openDb(dbName, storeNames, version = 1) {
    if (_db)
        return Promise.resolve(_db)

    return new Promise((resolve, reject) => {
        const req = indexedDB.open(dbName, version)

        req.onupgradeneeded = (ev) => {
            const db = ev.target.result
            
            for (const storeName of storeNames) {
                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName)
                }
            }
        }
        
        req.onsuccess = () => {
            _db = req.result
            _db.onclose = () => { _db = null }
            resolve(_db)
        }
        
        req.onerror = () => reject(req.error)
    })
}

function withStore(storeName, mode, work) {
    if (!_db)
        return Promise.reject(new Error("DB not opened. Call init first."))
    
    const tx = _db.transaction(storeName, mode)
    const store = tx.objectStore(storeName)
    
    return work(store).then(
        (res) => new Promise((resolve, reject) => {
            tx.oncomplete = () => resolve(res)
            tx.onerror = () => reject(tx.error)
            tx.onabort = () => reject(tx.error || new Error("Transaction aborted"))
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function init(dbName, storeNames, version) {
    await openDb(dbName, storeNames, version || 1)
    return true
}

// noinspection JSUnusedGlobalSymbols
export async function set(storeName, key, value) {
    return withStore(storeName, "readwrite", (store) =>
        new Promise((resolve, reject) => {
            const req = store.put(value, key)
            req.onsuccess = () => resolve(true)
            req.onerror = () => reject(req.error)
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function get(storeName, key) {
    return withStore(storeName, "readonly", (store) =>
        new Promise((resolve, reject) => {
            const req = store.get(key)
            req.onsuccess = () => resolve(req.result ?? null)
            req.onerror = () => reject(req.error)
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function getAll(storeName) {
    return withStore(storeName, "readonly", (store) =>
        new Promise((resolve, reject) => {
            if (store.getAll) {
                const req = store.getAll()
                req.onsuccess = () =>  {
                    // console.log(req.result)
                    resolve(req.result ?? [])
                }
                req.onerror = () => reject(req.error)
            } else {
                // Fallback for Safari < 14
                const items = []
                const req = store.openCursor()
                req.onsuccess = (ev) => {
                    const cursor = ev.target.result
                    if (cursor) {
                        items.push(cursor.value)
                        cursor.continue()
                    } else {
                        resolve(items)
                    }
                }
                req.onerror = () => reject(req.error)
            }
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function del(storeName, key) {
    return withStore(storeName, "readwrite", (store) =>
        new Promise((resolve, reject) => {
            const req = store.delete(key)
            req.onsuccess = () => resolve(true)
            req.onerror = () => reject(req.error)
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function keys(storeName) {
    return withStore(storeName, "readonly", (store) =>
        new Promise((resolve, reject) => {
            const ks = []
            // Support both getAllKeys and openKeyCursor for older Safari
            if (store.getAllKeys) {
                const req = store.getAllKeys()
                req.onsuccess = () => resolve(req.result || [])
                req.onerror = () => reject(req.error)
            } else {
                const req = store.openKeyCursor()
                req.onsuccess = (ev) => {
                    const cursor = ev.target.result
                    if (cursor) {
                        ks.push(cursor.key)
                        cursor.continue()
                    } else
                        resolve(ks)
                }
                req.onerror = () => reject(req.error)
            }
        })
    )
}

// noinspection JSUnusedGlobalSymbols
export async function clear(storeName) {
    return withStore(storeName, "readwrite", (store) =>
        new Promise((resolve, reject) => {
            const req = store.clear()
            req.onsuccess = () => resolve(true)
            req.onerror = () => reject(req.error)
        })
    )
}
