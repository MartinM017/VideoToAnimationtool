#include <opencv2/imgcodecs.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/photo.hpp>

#include <iostream>
#include <string>
#include <vector>

static double parseRadius(int argc, char** argv)
{
    if (argc < 5) return 12.0;
    const double radius = std::atof(argv[4]);
    if (radius < 1.0) return 1.0;
    if (radius > 50.0) return 50.0;
    return radius;
}

static int parseMaskExpand(int argc, char** argv)
{
    if (argc < 6) return 4;
    const int expand = std::atoi(argv[5]);
    if (expand < 0) return 0;
    if (expand > 40) return 40;
    return expand;
}

int main(int argc, char** argv)
{
    if (argc < 4)
    {
        std::cerr << "Usage: opencv-inpaint-helper <input> <output> <mask> [radius] [mask-expand]\n";
        return 2;
    }

    const std::string inputPath = argv[1];
    const std::string outputPath = argv[2];
    const std::string maskPath = argv[3];
    const double radius = parseRadius(argc, argv);
    const int maskExpand = parseMaskExpand(argc, argv);

    cv::Mat input = cv::imread(inputPath, cv::IMREAD_UNCHANGED);
    cv::Mat mask = cv::imread(maskPath, cv::IMREAD_GRAYSCALE);
    if (input.empty())
    {
        std::cerr << "Failed to read input image: " << inputPath << "\n";
        return 3;
    }
    if (mask.empty())
    {
        std::cerr << "Failed to read mask image: " << maskPath << "\n";
        return 4;
    }

    if (mask.size() != input.size())
        cv::resize(mask, mask, input.size(), 0, 0, cv::INTER_NEAREST);
    cv::threshold(mask, mask, 1, 255, cv::THRESH_BINARY);
    if (maskExpand > 0)
    {
        const int kernelSize = (maskExpand * 2) + 1;
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(kernelSize, kernelSize));
        cv::dilate(mask, mask, kernel);
    }
    cv::Mat closeKernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(3, 3));
    cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, closeKernel);

    cv::Mat output;
    if (input.channels() == 4)
    {
        std::vector<cv::Mat> channels;
        cv::split(input, channels);
        cv::Mat bgr;
        cv::cvtColor(input, bgr, cv::COLOR_BGRA2BGR);
        cv::Mat inpaintedBgr;
        cv::inpaint(bgr, mask, inpaintedBgr, radius, cv::INPAINT_TELEA);

        cv::Mat inpaintedAlpha;
        cv::inpaint(channels[3], mask, inpaintedAlpha, radius, cv::INPAINT_TELEA);

        std::vector<cv::Mat> bgrChannels;
        cv::split(inpaintedBgr, bgrChannels);
        std::vector<cv::Mat> outChannels;
        outChannels.push_back(bgrChannels[0]);
        outChannels.push_back(bgrChannels[1]);
        outChannels.push_back(bgrChannels[2]);
        outChannels.push_back(inpaintedAlpha);
        cv::merge(outChannels, output);
    }
    else if (input.channels() == 3)
    {
        cv::inpaint(input, mask, output, radius, cv::INPAINT_TELEA);
    }
    else
    {
        cv::Mat bgr;
        cv::cvtColor(input, bgr, cv::COLOR_GRAY2BGR);
        cv::inpaint(bgr, mask, output, radius, cv::INPAINT_TELEA);
    }

    if (!cv::imwrite(outputPath, output))
    {
        std::cerr << "Failed to write output image: " << outputPath << "\n";
        return 5;
    }

    return 0;
}
