/** @type import('hardhat/config').HardhatUserConfig */
const config = {
  solidity: "0.8.20",
  paths: {
    sources: "./contracts",
  },
  networks: {
    monadMainnet: {
      url: "https://rpc.monad.xyz",
      chainId: 143,
    },
  },
};

export default config;
