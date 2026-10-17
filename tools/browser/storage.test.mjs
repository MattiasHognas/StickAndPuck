import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createSaveStore } from '../../src/StickAndPuck.App/wwwroot/storage.js';

function memoryStorage() {
    const data = new Map();
    return { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value), removeItem: key => data.delete(key) };
}

test('settings, season and arcade survive another store instance without sharing keys', () => {
    const storage = memoryStorage();
    const store = createSaveStore(storage, () => {});
    for (const name of ['settings', 'season', 'arcade']) store.write(`/saves/${name}.json`, JSON.stringify({ name }));
    const reloaded = createSaveStore(storage, () => {});
    for (const name of ['settings', 'season', 'arcade']) assert.equal(reloaded.read(`/saves/${name}.json`), JSON.stringify({ name }));
    reloaded.delete('/saves/season.json');
    assert.equal(reloaded.exists('/saves/season.json'), false);
    assert.equal(reloaded.exists('/saves/arcade.json'), true);
});

test('a rejected write reports failure and keeps the previous save', () => {
    const storage = memoryStorage();
    const messages = [];
    const store = createSaveStore(storage, value => messages.push(value));
    store.write('/saves/season.json', 'old');
    storage.setItem = () => { throw new Error('QuotaExceededError'); };
    assert.throws(() => store.write('/saves/season.json', 'new'), /QuotaExceededError/);
    assert.equal(store.read('/saves/season.json'), 'old');
    assert.match(messages.at(-1), /Browser save failed/);
});
