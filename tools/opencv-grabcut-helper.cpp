#include <opencv2/imgcodecs.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/core.hpp>

#include <iostream>
#include <string>

static int clampInt(int value, int low, int high)
{
    return std::max(low, std::min(high, value));
}

int main(int argc, char** argv)
{
    if (argc < 3)
    {
        std::cerr << "Usage: opencv-grabcut-helper <input> <output> [iterations] [initial-alpha-mask]\n";
        return 2;
    }

    const std::string inputPath = argv[1];
    const std::string outputPath = argv[2];
    const int iterations = argc >= 4 ? clampInt(std::atoi(argv[3]), 1, 10) : 5;
    const std::string maskPath = argc >= 5 ? argv[4] : std::string();

    cv::Mat input = cv::imread(inputPath, cv::IMREAD_UNCHANGED);
    if (input.empty())
    {
        std::cerr << "Failed to read input image: " << inputPath << "\n";
        return 3;
    }

    cv::Mat bgr;
    if (input.channels() == 4)
        cv::cvtColor(input, bgr, cv::COLOR_BGRA2BGR);
    else if (input.channels() == 3)
        bgr = input;
    else
        cv::cvtColor(input, bgr, cv::COLOR_GRAY2BGR);

    const int marginX = std::max(2, bgr.cols / 18);
    const int marginY = std::max(2, bgr.rows / 18);
    cv::Rect subjectRect(marginX, marginY, std::max(1, bgr.cols - marginX * 2), std::max(1, bgr.rows - marginY * 2));

    cv::Mat mask(bgr.size(), CV_8UC1, cv::Scalar(cv::GC_BGD));
    int mode = cv::GC_INIT_WITH_RECT;
    if (!maskPath.empty())
    {
        cv::Mat initialMask = cv::imread(maskPath, cv::IMREAD_GRAYSCALE);
        if (initialMask.empty())
        {
            std::cerr << "Failed to read initial mask: " << maskPath << "\n";
            return 6;
        }
        if (initialMask.size() != bgr.size())
            cv::resize(initialMask, initialMask, bgr.size(), 0, 0, cv::INTER_NEAREST);

        mask.setTo(cv::Scalar(cv::GC_BGD));
        for (int y = 0; y < initialMask.rows; ++y)
        {
            for (int x = 0; x < initialMask.cols; ++x)
            {
                const uchar alpha = initialMask.at<uchar>(y, x);
                if (alpha >= 245) mask.at<uchar>(y, x) = cv::GC_FGD;
                else if (alpha >= 32) mask.at<uchar>(y, x) = cv::GC_PR_FGD;
                else mask.at<uchar>(y, x) = cv::GC_BGD;
            }
        }

        int minX = bgr.cols;
        int minY = bgr.rows;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < initialMask.rows; ++y)
        {
            for (int x = 0; x < initialMask.cols; ++x)
            {
                if (initialMask.at<uchar>(y, x) > 32)
                {
                    minX = std::min(minX, x);
                    minY = std::min(minY, y);
                    maxX = std::max(maxX, x);
                    maxY = std::max(maxY, y);
                }
            }
        }

        if (maxX >= minX && maxY >= minY)
        {
            minX = std::max(0, minX - 6);
            minY = std::max(0, minY - 6);
            maxX = std::min(bgr.cols - 1, maxX + 6);
            maxY = std::min(bgr.rows - 1, maxY + 6);
            subjectRect = cv::Rect(minX, minY, std::max(1, maxX - minX + 1), std::max(1, maxY - minY + 1));
        }

        mode = cv::GC_INIT_WITH_MASK;
    }
    else
    {
        mask(subjectRect).setTo(cv::Scalar(cv::GC_PR_FGD));
    }

    cv::Mat bgdModel;
    cv::Mat fgdModel;
    try
    {
        cv::grabCut(bgr, mask, subjectRect, bgdModel, fgdModel, iterations, mode);
    }
    catch (const cv::Exception& ex)
    {
        std::cerr << "OpenCV grabCut failed: " << ex.what() << "\n";
        return 4;
    }

    cv::Mat foregroundMask = (mask == cv::GC_FGD) | (mask == cv::GC_PR_FGD);
    cv::morphologyEx(foregroundMask, foregroundMask, cv::MORPH_OPEN, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(3, 3)));
    cv::morphologyEx(foregroundMask, foregroundMask, cv::MORPH_CLOSE, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(3, 3)));
    cv::GaussianBlur(foregroundMask, foregroundMask, cv::Size(3, 3), 0);

    cv::Mat bgra;
    cv::cvtColor(bgr, bgra, cv::COLOR_BGR2BGRA);
    std::vector<cv::Mat> channels;
    cv::split(bgra, channels);
    channels[3] = foregroundMask;
    cv::merge(channels, bgra);

    if (!cv::imwrite(outputPath, bgra))
    {
        std::cerr << "Failed to write output image: " << outputPath << "\n";
        return 5;
    }

    return 0;
}
