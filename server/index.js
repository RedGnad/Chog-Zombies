const express = require('express');
const cors = require('cors');
const { Pool } = require('pg');

const app = express();
app.use(cors());
app.use(express.json());

const port = process.env.PORT || 3000;

let pool = null;
if (process.env.DATABASE_URL) {
  pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    ssl: process.env.NODE_ENV === 'production' ? { rejectUnauthorized: false } : false,
  });
}

function normalizeWallet(address) {
  if (!address) return null;
  return address.toLowerCase();
}

async function getUser(wallet) {
  if (!pool) return null;
  const client = await pool.connect();
  try {
    const result = await client.query(
      'select wallet_address, owned_items, equipped_items, run_base_seed, gold from users where wallet_address = $1',
      [wallet]
    );
    if (result.rows.length === 0) return null;
    const row = result.rows[0];
    return {
      walletAddress: row.wallet_address,
      ownedItems: row.owned_items || [],
      equippedItems: row.equipped_items || [],
      runBaseSeed: row.run_base_seed == null ? 0 : Number(row.run_base_seed),
      gold: row.gold == null ? 0 : Number(row.gold),
    };
  } finally {
    client.release();
  }
}

async function upsertUserLoot(wallet, ownedItems, equippedItems) {
  if (!pool) return;
  const client = await pool.connect();
  try {
    await client.query(
      'insert into users (wallet_address, owned_items, equipped_items) values ($1, $2, $3) on conflict (wallet_address) do update set owned_items = excluded.owned_items, equipped_items = excluded.equipped_items',
      [wallet, ownedItems, equippedItems]
    );
  } finally {
    client.release();
  }
}

async function upsertUserState(wallet, runBaseSeed, gold) {
  if (!pool) return;
  const client = await pool.connect();
  try {
    await client.query(
      'insert into users (wallet_address, run_base_seed, gold) values ($1, $2, $3) on conflict (wallet_address) do update set run_base_seed = excluded.run_base_seed, gold = excluded.gold',
      [wallet, runBaseSeed, gold]
    );
  } finally {
    client.release();
  }
}

app.get('/health', (req, res) => {
  res.json({ status: 'ok' });
});

app.get('/users/:walletAddress', async (req, res) => {
  try {
    const wallet = normalizeWallet(req.params.walletAddress);
    if (!wallet) {
      res.status(400).json({ error: 'invalid_wallet_address' });
      return;
    }
    const user = await getUser(wallet);
    if (!user) {
      res.json({
        walletAddress: wallet,
        ownedItems: [],
        equippedItems: [],
        runBaseSeed: 0,
        gold: 0,
      });
      return;
    }
    res.json(user);
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: 'internal_error' });
  }
});

app.put('/users/:walletAddress/state', async (req, res) => {
  try {
    const wallet = normalizeWallet(req.params.walletAddress);
    if (!wallet) {
      res.status(400).json({ error: 'invalid_wallet_address' });
      return;
    }

    const runBaseSeedRaw = req.body.runBaseSeed;
    const goldRaw = req.body.gold;

    const runBaseSeed =
      typeof runBaseSeedRaw === 'number' && Number.isFinite(runBaseSeedRaw)
        ? Math.trunc(runBaseSeedRaw)
        : null;
    const gold =
      typeof goldRaw === 'number' && Number.isFinite(goldRaw)
        ? Math.trunc(goldRaw)
        : 0;

    await upsertUserState(wallet, runBaseSeed, gold);

    res.json({ walletAddress: wallet, runBaseSeed, gold });
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: 'internal_error' });
  }
});

app.put('/users/:walletAddress/loot', async (req, res) => {
  try {
    const wallet = normalizeWallet(req.params.walletAddress);
    if (!wallet) {
      res.status(400).json({ error: 'invalid_wallet_address' });
      return;
    }
    const ownedItems = Array.isArray(req.body.ownedItems) ? req.body.ownedItems : [];
    const equippedItems = Array.isArray(req.body.equippedItems) ? req.body.equippedItems : [];
    await upsertUserLoot(wallet, ownedItems, equippedItems);
    res.json({ walletAddress: wallet, ownedItems, equippedItems });
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: 'internal_error' });
  }
});

app.listen(port, () => {
  console.log(`Server listening on port ${port}`);
});
