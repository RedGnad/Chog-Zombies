import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import process from "process";
import { ethers } from "ethers";
import dotenv from "dotenv";

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const MONAD_RPC_URL = process.env.MONAD_RPC_URL || "https://rpc.monad.xyz";
const MONAD_CHAIN_ID = 143;
const ENTROPY_V2_ADDRESS = "0xd458261e832415cfd3bae5e416fdf3230ce6f134";

async function main() {
  const privateKey = process.env.MONAD_PRIVATE_KEY;
  if (!privateKey) {
    throw new Error("MONAD_PRIVATE_KEY must be set in the environment");
  }

  const provider = new ethers.JsonRpcProvider(MONAD_RPC_URL, MONAD_CHAIN_ID);
  const wallet = new ethers.Wallet(privateKey, provider);

  console.log("Deploying ChogZombiesEntropyConsumer with:");
  console.log("  RPC:", MONAD_RPC_URL);
  console.log("  ChainId:", MONAD_CHAIN_ID);
  console.log("  Deployer:", await wallet.getAddress());

  const artifactPath = path.join(
    __dirname,
    "..",
    "artifacts",
    "contracts",
    "ChogZombiesEntropyConsumer.sol",
    "ChogZombiesEntropyConsumer.json"
  );

  if (!fs.existsSync(artifactPath)) {
    throw new Error(
      `Artifact not found at ${artifactPath}. Did you run \\"npx hardhat compile\\"?`
    );
  }

  const artifact = JSON.parse(fs.readFileSync(artifactPath, "utf8"));
  const { abi, bytecode } = artifact;

  const factory = new ethers.ContractFactory(abi, bytecode, wallet);

  console.log("Using Entropy V2 address:", ENTROPY_V2_ADDRESS);
  const contract = await factory.deploy(ENTROPY_V2_ADDRESS);

  const tx = contract.deploymentTransaction();
  console.log("Deployment tx hash:", tx.hash);

  await contract.waitForDeployment();
  const contractAddress = await contract.getAddress();

  console.log("ChogZombiesEntropyConsumer deployed at:", contractAddress);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
