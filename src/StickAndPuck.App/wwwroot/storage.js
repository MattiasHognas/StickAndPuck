const prefix = 'stickandpuck:v1:';

// A setItem is an atomic, synchronous commit. No page-unload flush or timer is needed.
export function createSaveStore(storage, report) {
    const key = path => prefix + path;
    function access(action) {
        try {
            return action();
        } catch (error) {
            report(`Browser save failed: ${error.message}. Check storage permissions and available space.`);
            throw error;
        }
    }
    return {
        exists: path => access(() => storage.getItem(key(path)) !== null),
        read: path => access(() => {
            const value = storage.getItem(key(path));
            if (value === null) throw new Error(`Save does not exist: ${path}`);
            return value;
        }),
        write: (path, value) => access(() => {
            storage.setItem(key(path), value);
            report('Saved on this browser.');
        }),
        delete: path => access(() => storage.removeItem(key(path)))
    };
}
