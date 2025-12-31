/** @type {import('next').NextConfig} */
const nextConfig = {
  async redirects() {
    return [
      {
        source: '/ads.txt',
        destination: 'https://srv.adstxtmanager.com/82008/itims.online',
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
